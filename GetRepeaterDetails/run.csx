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
    try {
        var dataTable = new DataTable();

        string strSql = "EXEC dbo.spGetRepeaterDetails @callsign, @password, @repeaterid";

        var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
        using (SqlConnection Connection = new SqlConnection(ConnectionString))
        {
            Connection.Open();
            SqlCommand cmd = new SqlCommand(strSql, Connection);

            addParameter(cmd, req, "callsign", log);
            addParameter(cmd, req, "password", log);
            addParameter(cmd, req, "repeaterid", log);

            SqlDataReader rdr = cmd.ExecuteReader();
            dataTable.Load(rdr);

            rdr.Close();
            Connection.Close();
        }
    }
    catch (Exception ex) {
        log.Error(string.Format("Exception 1: {0}\r\n\r\n{1}", ex.Message, req.Content.ReadAsStringAsync().Result));
    }

    try {
        var firstRow = JArray.FromObject(dataTable, JsonSerializer.CreateDefault(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })).FirstOrDefault(); // Get the first row            
        var json = firstRow.ToString(); 

        //string json = Newtonsoft.Json.JsonConvert.SerializeObject(dataTable, Newtonsoft.Json.Formatting.Indented);
        return new HttpResponseMessage(HttpStatusCode.OK) 
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
    catch (Exception ex) {
        log.Error(string.Format("Exception 2: {0}\r\n\r\n{1}", ex.Message, req.Content.ReadAsStringAsync().Result));
    }
}

public static void addParameter(SqlCommand cmd, HttpRequestMessage req, string keyName, TraceWriter log) {
    string val = "";

    try {
    val = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (val == null) { val = ""; }

    cmd.Parameters.AddWithValue("@" + keyName, val);
    }
    catch (Exception ex) {
        log.Error(string.Format("Exception 2: {0}\r\n\r\n{1}", ex.Message, req.Content.ReadAsStringAsync().Result));
    }
}