using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// Required Application Settings:
//   PayPalVerifyUrl    - "https://ipnpb.paypal.com/cgi-bin/webscr" (live)
//                        "https://ipnpb.sandbox.paypal.com/cgi-bin/webscr" (sandbox)
//   PayPalMerchantId   - PayPal merchant ID
//   Database           - SQL connection string for the repeater database

public static readonly HttpClient _httpClient = new HttpClient();

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // 1. Read the raw IPN body
    string ipnBody = await req.Content.ReadAsStringAsync();

    log.Info("PayPal IPN received: " + ipnBody);

    // 2. Verify the IPN with PayPal
    bool verified = await VerifyIpnAsync(ipnBody, log);
    if (!verified)
    {
        log.Warning("PayPal IPN verification failed.");
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    // 3. Parse IPN fields
    var fields = ParseQueryString(ipnBody);

    string paymentStatus = GetField(fields, "payment_status");
    string receiverId = GetField(fields, "receiver_id");
    string repeaterId = GetField(fields, "custom");
    string txnId = GetField(fields, "txn_id");
    string mc_gross = GetField(fields, "mc_gross");
    string mc_currency = GetField(fields, "mc_currency");

    // 4. Validate receiver ID to prevent payment hijacking
    string expectedId = ConfigurationManager.AppSettings["PayPalMerchantId"] ?? string.Empty;
    if (!string.Equals(receiverId, expectedId, StringComparison.OrdinalIgnoreCase))
    {
        log.Warning("IPN receiver_id mismatch. Expected: " + expectedId + ", Got: " + receiverId);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    // 5. Only process completed payments
    if (!string.Equals(paymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
    {
        log.Info("IPN payment_status is '" + paymentStatus + "' — skipping.");
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    // 6. Validate amount
    decimal amount;
    if (!decimal.TryParse(mc_gross, out amount) || amount < 5.00M || mc_currency != "USD")
    {
        log.Warning("IPN amount/currency validation failed. gross=" + mc_gross + ", currency=" + mc_currency);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    // 7. Validate repeater ID
    int repeaterIdInt;
    if (string.IsNullOrWhiteSpace(repeaterId) || !int.TryParse(repeaterId, out repeaterIdInt))
    {
        log.Warning("IPN missing or invalid repeater ID in 'custom' field: '" + repeaterId + "'");
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    // 8. Calculate fee paid through date (3 years from today)
    string feePaidThrough = DateTime.UtcNow.AddYears(3).ToString("yyyy-MM-dd");

    // 9. Update FeePaidThrough
    bool updated = await UpdateFeePaidThroughAsync(repeaterId, feePaidThrough, txnId, log);
    if (!updated)
    {
        log.Error("Failed to update FeePaidThrough for repeater " + repeaterId + ". Manual follow-up required. TxnId=" + txnId);
    }
    else
    {
        log.Info("FeePaidThrough updated to " + feePaidThrough + " for repeater " + repeaterId + " (txn " + txnId + ")");
    }

    return new HttpResponseMessage(HttpStatusCode.OK);
}

public static async Task<bool> VerifyIpnAsync(string ipnBody, TraceWriter log)
{
    try
    {
        string verifyUrl = ConfigurationManager.AppSettings["PayPalVerifyUrl"]
            ?? "https://ipnpb.paypal.com/cgi-bin/webscr";

        string verifyBody = "cmd=_notify-validate&" + ipnBody;

        var content = new StringContent(verifyBody, Encoding.ASCII, "application/x-www-form-urlencoded");
        var response = await _httpClient.PostAsync(verifyUrl, content);
        string responseText = await response.Content.ReadAsStringAsync();

        return string.Equals(responseText.Trim(), "VERIFIED", StringComparison.OrdinalIgnoreCase);
    }
    catch (Exception ex)
    {
        log.Error("Exception during PayPal IPN verification: " + ex.Message);
        return false;
    }
}

public static async Task<bool> UpdateFeePaidThroughAsync(string repeaterId, string feePaidThrough, string txnId, TraceWriter log)
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
        log.Error("Exception updating FeePaidThrough for repeater " + repeaterId + ": " + ex.Message);
        return false;
    }
}

public static Dictionary<string, string> ParseQueryString(string queryString)
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

public static string GetField(Dictionary<string, string> fields, string key)
{
    string val;
    return fields.TryGetValue(key, out val) ? val : string.Empty;
}
