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

    string strSql = "EXEC dbo.spListPublicRepeaters @state, @search, @latitude, @longitude, @miles, @pageSize, @pageNumber, @orderBy";

    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    using (SqlConnection Connection = new SqlConnection(ConnectionString))
    {
        Connection.Open();
        SqlCommand cmd = new SqlCommand(strSql, Connection);

        addParameter(cmd, req, "state");
        addParameter(cmd, req, "search");
        addParameter(cmd, req, "latitude", "39.83");
        addParameter(cmd, req, "longitude", "-98.583");
        addParameter(cmd, req, "miles", "2680");
        addParameter(cmd, req, "pageSize", "1000");
        addParameter(cmd, req, "pageNumber", "1");
        addParameter(cmd, req, "orderBy", "OutputFrequency");

        SqlDataReader rdr = cmd.ExecuteReader();
        dataTable.Load(rdr);

        rdr.Close();
        Connection.Close();
    }

    string json = Newtonsoft.Json.JsonConvert.SerializeObject(dataTable, Newtonsoft.Json.Formatting.Indented);
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

public static void addParameter(SqlCommand cmd, HttpRequestMessage req, string keyName, string defaultValue) {
    string val = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (val == null) { val = defaultValue; }

    cmd.Parameters.AddWithValue("@" + keyName, val);
}

public static void addParameters(SqlCommand cmd, HttpRequestMessage req, TraceWriter log) {
    // Get request body
    string data = req.Content.ReadAsStringAsync().Result;

    using (var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(data)))
    {
        while (reader.Read())
        {
            string propertyName = String.Empty;
            string propertyValue = String.Empty;
            if (reader.TokenType.ToString() == "PropertyName") {
                propertyName = reader.Value.ToString();

                reader.Read();
                if (reader.Value == null) {
                    propertyValue = String.Empty;
                }
                else {
                    propertyValue = reader.Value.ToString();
                }

                cmd.Parameters.AddWithValue("@" + propertyName, propertyValue);
            }
        }
    }
}