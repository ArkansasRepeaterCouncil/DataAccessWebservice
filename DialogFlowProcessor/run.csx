#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Xml;
using System.Text;

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

public static string callsignLookup(string callsign, ILogger log)
{
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

    return string.Format("{0}{1} is assigned to {2}.  It will expire on {3}.  {4}", callsign, licClass, name, expireDate, email);
}

public static string GetXmlValue(XmlDocument xDoc, string element) 
{
    string strReturn = "";

    if (xDoc.GetElementsByTagName(element).Count > 0) {
        strReturn = xDoc.GetElementsByTagName(element)[0].InnerText;
    }

    return strReturn;
}
