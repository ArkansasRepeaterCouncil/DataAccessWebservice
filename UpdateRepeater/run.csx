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

    string strSql = "EXEC dbo.spUpdateRepeater @callsign, @password, @ID, @type, @RepeaterCallsign, @trusteeID, @status, @city, @siteName, @OutputFrequency, @InputFrequency, @latitude, @longitude, @sponsor, @amsl, @erp, @outputPower, @antennaGain, @antennaHeight, @Analog_InputAccess, @Analog_OutputAccess, @Analog_Width, @DSTAR_Module, @DMR_ColorCode, @DMR_ID, @DMR_Network, @P25_NAC, @NXDN_RAN, @YSF_DSQ, @autopatch, @emergencyPower, @linked, @races, @ares, @wideArea, @weather, @experimental, @changelog, @dateupdated, @additionalInformation";

    var ConnectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
    try 
    {
        using (SqlConnection Connection = new SqlConnection(ConnectionString))
        {
            Connection.Open();
            SqlCommand cmd = new SqlCommand(strSql, Connection);

            addParameters(cmd, req, log);

            SqlDataReader rdr = cmd.ExecuteReader();
            dataTable.Load(rdr);

            rdr.Close();
            Connection.Close();
        }
    }
    catch(Exception ex) 
    {
        log.Error(string.Format("Exception: {0}\r\n\r\n{1}", ex.Message, req.Content.ReadAsStringAsync().Result));
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
                log.Info("@" + propertyName + " = " + propertyValue);
                cmd.Parameters.AddWithValue("@" + propertyName, propertyValue);
            }
        }
    }
}