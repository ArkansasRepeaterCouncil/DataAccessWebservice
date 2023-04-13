#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Xml;
using System.Text;
using System.Data;
using System.Data.SqlClient;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    string response = "";

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    log.LogInformation(requestBody);
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    string action = (data.queryResult.action).ToString();

    log.LogInformation("action: " + action);
    switch (action) {
        case "callsignLookup":
            response = callsignLookup((data.queryResult.parameters.callsign).ToString(), log);
            break;
        default:
            response = "I'm not sure how to help you with that.";
            string queryResult = (data.queryResult).ToString();
            log.LogError(queryResult);
            break;
    }

    return (ActionResult)new OkObjectResult("{ \"fulfillmentMessages\": [{\"text\": {\"text\": [\"" + response + "\"]}}]}");
}
public static string callsignLookup(string callsign, ILogger log) {
    callsign = callsign.ToUpper().Replace("?","");

    // Load application variables
    string qrzLoginUsername = Environment.GetEnvironmentVariable("qrzLoginUsername");
    string qrzLoginPassword = Environment.GetEnvironmentVariable("qrzLoginPassword");

	// Get a QRZ session key
    XmlDocument xDocKey = new XmlDocument();
    xDocKey.Load("https://xmldata.qrz.com/xml/?username=" + qrzLoginUsername + "&password=" + qrzLoginPassword);
    string qrzKey = xDocKey.GetElementsByTagName("Key")[0].InnerText;

    // Send request to QRZ for data on this callsign
    XmlDocument xDoc = new XmlDocument();
    xDoc.Load("https://xmldata.qrz.com/xml/current/?s=" + qrzKey + "&callsign=" + callsign);
    
    string name = string.Format("{0} {1}", GetXmlValue(xDoc, "fname"), GetXmlValue(xDoc, "name"));
    string expireDate = GetXmlValue(xDoc, "expdate");
    string address = string.Format("{0}, {1}, {2} {3}", GetXmlValue(xDoc, "addr1"), GetXmlValue(xDoc, "addr2"), GetXmlValue(xDoc, "state"), GetXmlValue(xDoc, "zip"));
    string licClass = GetXmlValue(xDoc, "class");
    switch (licClass.ToLower()) {
        case "e":
            licClass = ", an Extra class license,";
            break;
        case "g":
            licClass = ", a General class license,";
            break;
        case "n":
            licClass = ", a Novice class license,";
            break;
        case "t":
            licClass = ", a Technician class license,";
            break;
        default:
            licClass = "";
            break;
    }
    string email = GetXmlValue(xDoc, "email");
    if (email.Trim() != "") {
        email = string.Format("  The email address they have on file is {0}.", email);
    }

	string userInfoJson = "";
	string userInfoDescription = "";
    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    using (SqlConnection Connection = new SqlConnection(ConnectionString))
    {
        Connection.Open();
        SqlCommand cmd = new SqlCommand("EXEC dbo.sp_QueryCallsign @callsign", Connection);
		cmd.Parameters.AddWithValue("@callsign", callsign);
        userInfoJson = cmd.ExecuteScalar();
        Connection.Close();
    }
	if (userInfoJson != "") {
		var dynObj = JsonConvert.DeserializeObject<dynamic>(userInfoJson);
		userInfoDescription = string.Format("Our database shows that {0} belongs to {1}, who is associated with {2} repeaters.", callsign, dynObj.User.Name, dynObj.User.Repeaters.Count);
		if ((dynObj.User.Phone.Home + dynObj.User.Phone.Work + dynObj.User.Phone.Cell).Trim() != "") {
			userInfoDescription += "  Here are the phone numbers we have on file: ";
			if (dynObj.User.Phone.Home != "") { userInfoDescription += dynObj.User.Phone.Home + " (home)"; }
			if (dynObj.User.Phone.Work != "") {
				if (dynObj.User.Phone.Home != "") { userInfoDescription += ", "; }
				userInfoDescription += dynObj.User.Phone.Work + " (work)";
			}
			if (dynObj.User.Phone.Cell != "") {
				if (dynObj.User.Phone.Home + dynObj.User.Phone.Work != "") { userInfoDescription += ", "; }
				userInfoDescription += dynObj.User.Phone.Cell + " (cell)";
			}
			userInfoDescription += ".";
		}
		if (dynObj.User.Email != "") { userInfoDescription += "  The email address we have for them is " + dynObj.User.Email; }
	}
	
    return string.Format("According to QRZ, {0}{1} is assigned to {2}.  The license expiration date is {3}.{4}{5}", callsign, licClass, name, expireDate, email, userInfoDescription);
}

// Utility functions
public static string GetXmlValue(XmlDocument xDoc, string element) {
    string strReturn = "";

    if (xDoc.GetElementsByTagName(element).Count > 0) {
        strReturn = xDoc.GetElementsByTagName(element)[0].InnerText;
    }

    return strReturn;
}
public static void addParameter(SqlCommand cmd, HttpRequestMessage req, string keyName) {
    string val = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (val == null) { val = ""; }

    cmd.Parameters.AddWithValue("@" + keyName, val);
}