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

    string strSql = "EXEC dbo.spProposedCoordination @callsign, @password, @Latitude, @Longitude, @TransmitFrequency, @ReceiveFrequency";
	
    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    using (SqlConnection Connection = new SqlConnection(ConnectionString))
    {
        Connection.Open();
        SqlCommand cmd = new SqlCommand(strSql, Connection);

        // addParameter(cmd, req, "callsign", log);
		// addParameter(cmd, req, "password", log);
		// addParameter(cmd, req, "Latitude", log);
        // addParameter(cmd, req, "Longitude", log);
        // addParameter(cmd, req, "TransmitFrequency", log);
		// addParameter(cmd, req, "ReceiveFrequency", log);
		addParameters(cmd, req, log);

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

public static void addParameter(SqlCommand cmd, HttpRequestMessage req, string keyName, TraceWriter log) {
    string val = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, keyName, true) == 0)
        .Value;

    if (val == null) { val = ""; }
	
	log.Info("Variable @" + keyName + " = " + val);
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
				
				log.Info("Variable @" + propertyName + " = " + propertyValue);
                cmd.Parameters.AddWithValue("@" + propertyName, propertyValue);
            }
        }
    }
}