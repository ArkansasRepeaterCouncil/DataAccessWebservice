using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    var dataTable = new DataTable();

    string strSql = "EXEC dbo.spKmlAllRepeaters @callsign, @password";

    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    using (SqlConnection Connection = new SqlConnection(ConnectionString))
    {
        Connection.Open();
        SqlCommand cmd = new SqlCommand(strSql, Connection);

        addParameter(cmd, req, "callsign");
        addParameter(cmd, req, "password");

        SqlDataReader rdr = cmd.ExecuteReader();
        dataTable.Load(rdr);

        rdr.Close();
        Connection.Close();
    }

    string output = "";
    for (int x = 0; x < dataTable.Rows.Count; x++) {
        output += dataTable.Rows[x][0].ToString();
    }

    output = output.Replace("<Document>", @"<Document><Style id=""33cm""><IconStyle><scale>1.0</scale><Icon><href>http://maps.google.com/mapfiles/kml/pushpin/blue-pushpin.png</href></Icon><hotSpot x=""20"" y=""2"" xunits=""pixels"" yunits=""pixels""/></IconStyle></Style><Style id=""70cm""><IconStyle><scale>1.0</scale><Icon><href>http://maps.google.com/mapfiles/kml/pushpin/grn-pushpin.png</href></Icon><hotSpot x=""20"" y=""2"" xunits=""pixels"" yunits=""pixels""/></IconStyle></Style><Style id=""1.25""><IconStyle><scale>1.0</scale><Icon><href>http://maps.google.com/mapfiles/kml/pushpin/red-pushpin.png</href></Icon><hotSpot x=""20"" y=""2"" xunits=""pixels"" yunits=""pixels""/></IconStyle></Style><Style id=""2m""><IconStyle><scale>1.0</scale><Icon><href>http://maps.google.com/mapfiles/kml/pushpin/purple-pushpin.png</href></Icon><hotSpot x=""20"" y=""2"" xunits=""pixels"" yunits=""pixels""/></IconStyle></Style><Style id=""6m""><IconStyle><scale>1.0</scale><Icon><href>http://maps.google.com/mapfiles/kml/pushpin/wht-pushpin.png</href></Icon><hotSpot x=""20"" y=""2"" xunits=""pixels"" yunits=""pixels""/></IconStyle></Style><Style id=""10m""><IconStyle><scale>1.0</scale><Icon><href>http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png</href></Icon><hotSpot x=""20"" y=""2"" xunits=""pixels"" yunits=""pixels""/></IconStyle></Style>");

    return new HttpResponseMessage(HttpStatusCode.OK) 
    {
        Content = new StringContent(output, Encoding.UTF8, "application/json")
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