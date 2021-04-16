using Newtonsoft.Json;
using System.Net;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
	
// Load application variables
    string qrzLoginUsername = Environment.GetEnvironmentVariable("qrzLoginUsername");
    string qrzLoginPassword = Environment.GetEnvironmentVariable("qrzLoginPassword");

// Get a QRZ session key
    XmlDocument xDocKey = new XmlDocument();
    xDocKey.Load("https://xmldata.qrz.com/xml/?username=" + qrzLoginUsername + "&password=" + qrzLoginPassword);
    string qrzKey = xDocKey.GetElementsByTagName("Key")[0].InnerText;
	
// Get all current callsigns
    var dataTable = new DataTable();

    string strSql = "Select callsign from users where callsign not like '%/SK'";
	
    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    using (SqlConnection Connection = new SqlConnection(ConnectionString))
    {
        Connection.Open();
        SqlCommand cmd = new SqlCommand(strSql, Connection);

        SqlDataReader rdr = cmd.ExecuteReader();
        dataTable.Load(rdr);

        rdr.Close();
        Connection.Close();
    }
	
// Loop through all callsigns
	for (int x = 0; x < dataTable.Rows.Count; x++) {
        // Send request to QRZ for data on this callsign
		// Make sure they aren't a silent key
		string callsign = dataTable.Rows[x][0];
		XmlDocument xDoc = new XmlDocument();
		xDoc.Load("https://xmldata.qrz.com/xml/current/?s=" + qrzKey + "&callsign=" + callsign);

		string qslmgr = ""; 
		string expdate = "";
		DateTime dtExpdate = new DateTime();

		try {
			qslmgr = xDoc.GetElementsByTagName("qslmgr")[0].InnerText;
		}
		catch {}

		try {
			expdate = xDoc.GetElementsByTagName("expdate")[0].InnerText;
			dtExpdate = DateTime.ParseExact(expdate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
		}
		catch {}

		if ((qslmgr != "") && (qslmgr.StartsWith("SK "))) { 
			log.Info(callsign + "/SK");
		} else if ((dtExpdate > DateTime.MinValue) && (dtExpdate < DateTime.Now)) {
			log.Info(callsign + " EXPIRED");
		}
    }

    return new HttpResponseMessage(HttpStatusCode.OK) 
    {
        Content = new StringContent("ok", Encoding.UTF8, "application/text")
    };
}

public static string getValue(HttpRequestMessage req, string keyName) {
    string rtn = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (rtn == null) { rtn = ""; }

    return rtn;
}
public static void addParameter(SqlCommand cmd, HttpRequestMessage req, string keyName) {
    string val = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (val == null) { val = ""; }

    cmd.Parameters.AddWithValue("@" + keyName, val);
}