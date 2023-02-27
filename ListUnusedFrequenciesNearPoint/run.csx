using Newtonsoft.Json;
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

    string strSql = "EXEC dbo.spListUnusedFrequenciesNearPoint @lat, @lon, @miles, @band";

    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    using (SqlConnection Connection = new SqlConnection(ConnectionString))
    {
        Connection.Open();
        SqlCommand cmd = new SqlCommand(strSql, Connection);

        addParameter(cmd, req, "lat");
        addParameter(cmd, req, "lon");
        addParameter(cmd, req, "miles");
        addParameter(cmd, req, "band");

		cmd.CommandTimeout = 60;
        SqlDataReader rdr = cmd.ExecuteReader();
        dataTable.Load(rdr);

        rdr.Close();
        Connection.Close();
    }

    string json = Newtonsoft.Json.JsonConvert.SerializeObject(dataTable, Newtonsoft.Json.Formatting.Indented);
	log.Info("OUTPUT: " + json);
    return new HttpResponseMessage(HttpStatusCode.OK) 
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}

public static void addParameter(SqlCommand cmd, HttpRequestMessage req, string keyName) {
    string val = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (val == null) { val = ""; }

	log.Info(keyname + " = " + val);
    cmd.Parameters.AddWithValue("@" + keyName, val);
}