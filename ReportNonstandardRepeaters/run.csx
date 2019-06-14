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

    string strSql = "EXEC dbo.ReportNonstandardRepeaters @callsign, @password";

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

    var firstRow = dataTable.Rows[0][0];
    //var firstRow = JArray.FromObject(dataTable, JsonSerializer.CreateDefault(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })).FirstOrDefault(); // Get the first row            
    var json = "{}";
    if (firstRow != null) {
        json = firstRow.ToString(); 
    }

    //string json = Newtonsoft.Json.JsonConvert.SerializeObject(dataTable, Newtonsoft.Json.Formatting.Indented);
    return new HttpResponseMessage(HttpStatusCode.OK) 
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
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