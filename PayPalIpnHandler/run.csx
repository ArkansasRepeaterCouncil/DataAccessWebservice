using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Azure Function that handles PayPal IPN (Instant Payment Notification) for coordination fee payments.
///
/// Required Application Settings:
///   PayPalVerifyUrl  - "https://ipnpb.paypal.com/cgi-bin/webscr" (live)
///                      "https://ipnpb.sandbox.paypal.com/cgi-bin/webscr" (sandbox)
///   PayPalMerchantId   - (PayPal merchant ID)
///   Database (connection string) - SQL connection string for the repeater database
/// </summary>
public static class PayPalIpnHandler
{
    private static readonly HttpClient _httpClient = new HttpClient();

    [FunctionName("PayPalIpn")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "paypal/ipn")] HttpRequest req,
        ILogger log)
    {
        // 1. Read the raw IPN body immediately
        string ipnBody;
        using (var reader = new StreamReader(req.Body, Encoding.ASCII))
        {
            ipnBody = await reader.ReadToEndAsync();
        }

        log.LogInformation("PayPal IPN received: {Body}", ipnBody);

        // 2. Return 200 OK to PayPal right away (required by PayPal IPN spec)
        //    Then verify asynchronously — but since Azure Functions are request/response,
        //    we verify before responding; PayPal allows up to 30 seconds per IPN spec.

        // 3. Verify the IPN with PayPal
        bool verified = await VerifyIpnAsync(ipnBody, log);
        if (!verified)
        {
            log.LogWarning("PayPal IPN verification failed.");
            return new OkResult(); // Always return 200 to PayPal, even on failure
        }

        // 4. Parse IPN fields
        var fields = ParseQueryString(ipnBody);

        string paymentStatus = GetField(fields, "payment_status");
        string receiverId = GetField(fields, "receiver_id");
        string repeaterId = GetField(fields, "custom");
        string txnId = GetField(fields, "txn_id");
        string mc_gross = GetField(fields, "mc_gross");
        string mc_currency = GetField(fields, "mc_currency");

        // 5. Validate receiver ID to prevent payment hijacking
        string expectedId = Environment.GetEnvironmentVariable("PayPalMerchantId") ?? string.Empty;
        if (!string.Equals(receiverId, expectedId, StringComparison.OrdinalIgnoreCase))
        {
            log.LogWarning("IPN receiver_id mismatch. Expected: {Expected}, Got: {Got}", expectedId, receiverId);
            return new OkResult();
        }

        // 6. Only process completed payments
        if (!string.Equals(paymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            log.LogInformation("IPN payment_status is '{Status}' — skipping.", paymentStatus);
            return new OkResult();
        }

        // 7. Validate amount
        if (!decimal.TryParse(mc_gross, out decimal amount) || amount < 5.00M || mc_currency != "USD")
        {
            log.LogWarning("IPN amount/currency validation failed. gross={Gross}, currency={Currency}", mc_gross, mc_currency);
            return new OkResult();
        }

        // 8. Validate repeater ID
        if (string.IsNullOrWhiteSpace(repeaterId) || !int.TryParse(repeaterId, out _))
        {
            log.LogWarning("IPN missing or invalid repeater ID in 'custom' field: '{RepeaterId}'", repeaterId);
            return new OkResult();
        }

        // 9. Calculate fee paid through date (3 years from today)
        string feePaidThrough = DateTime.UtcNow.AddYears(3).ToString("yyyy-MM-dd");

        // 10. Call the web service to update FeePaidThrough
        bool updated = await UpdateFeePaidThroughAsync(repeaterId, feePaidThrough, txnId, log);
        if (!updated)
        {
            log.LogError("Failed to update FeePaidThrough for repeater {RepeaterId}. Manual follow-up required. TxnId={TxnId}", repeaterId, txnId);
        }
        else
        {
            log.LogInformation("FeePaidThrough updated to {Date} for repeater {RepeaterId} (txn {TxnId})", feePaidThrough, repeaterId, txnId);
        }

        return new OkResult();
    }

    private static async Task<bool> VerifyIpnAsync(string ipnBody, ILogger log)
    {
        try
        {
            string verifyUrl = Environment.GetEnvironmentVariable("PayPalVerifyUrl")
                ?? "https://ipnpb.paypal.com/cgi-bin/webscr";

            string verifyBody = "cmd=_notify-validate&" + ipnBody;

            var content = new StringContent(verifyBody, Encoding.ASCII, "application/x-www-form-urlencoded");
            var response = await _httpClient.PostAsync(verifyUrl, content);
            string responseText = await response.Content.ReadAsStringAsync();

            return string.Equals(responseText.Trim(), "VERIFIED", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception during PayPal IPN verification.");
            return false;
        }
    }

    private static async Task<bool> UpdateFeePaidThroughAsync(string repeaterId, string feePaidThrough, string txnId, ILogger log)
    {
        try
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Repeaters SET FeePaidThrough = @FeePaidThrough, PayPalTxnId = @TxnId WHERE ID = @RepeaterId",
                    connection))
                {
                    cmd.Parameters.AddWithValue("@FeePaidThrough", feePaidThrough);
                    cmd.Parameters.AddWithValue("@TxnId", txnId);
                    cmd.Parameters.AddWithValue("@RepeaterId", int.Parse(repeaterId));
                    int rows = await cmd.ExecuteNonQueryAsync();
                    return rows > 0;
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception updating FeePaidThrough for repeater {RepeaterId}.", repeaterId);
            return false;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in queryString.Split('&'))
        {
            int eq = part.IndexOf('=');
            if (eq > 0)
            {
                string key = Uri.UnescapeDataString(part.Substring(0, eq).Replace("+", " "));
                string val = Uri.UnescapeDataString(part.Substring(eq + 1).Replace("+", " "));
                result[key] = val;
            }
        }
        return result;
    }

    private static string GetField(Dictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out string val) ? val : string.Empty;
}
