using System.Net;
using Microsoft.SyndicationFeed;
using Microsoft.SyndicationFeed.Rss;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http.Headers;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    var sw = new StringWriterWithEncoding(Encoding.UTF8);
    using (XmlWriter xmlWriter = XmlWriter.Create(sw, new XmlWriterSettings() { Async = true , Indent = true }))
    {
        var writer = new RssFeedWriter(xmlWriter);

        await writer.WriteTitle("Arkansas Repeater Changes");
        await writer.WriteDescription("Most recent changes made to Arkansas Repeater Council records.");
        await writer.Write(new SyndicationLink(new Uri("https://arkansasrepeatercouncil.org")));
        await writer.Write(new SyndicationPerson("managingeditor", "arkansasrepeaters@gmail.com (Arkansas Repeater Council)", RssContributorTypes.ManagingEditor));
        await writer.WritePubDate(DateTimeOffset.UtcNow);

        var dataTable = new DataTable();
        string strSql = "EXEC spListRecentChanges";
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

        foreach(DataRow row in dataTable.Rows)
        { 
            string ChangeID = "arkansasRepeaterCouncil.org-change" + row["ChangeID"].ToString();
            string RepeaterCallsign = row["RepeaterCallsign"].ToString();
            string City = row["City"].ToString();
            string State = row["State"].ToString();
            string Callsign = row["Callsign"].ToString();
            string FullName = row["FullName"].ToString();
            string Email = row["Email"].ToString();
            decimal decFrequency = (decimal)row["Frequency"];
            string Frequency = decFrequency.ToString("G29");
            DateTime ChangeDateTime = DateTime.Parse(row["ChangeDateTime"].ToString());
            string ChangeDescription = row["ChangeDescription"].ToString();

            var item = new SyndicationItem()
            {
                Id = ChangeID,
                Title = String.Format("{0} ({1}) in {2}, {3}", RepeaterCallsign, Frequency, City, State),
                Description = String.Format("{0} ({1}) made the following changes to the {2} repeater on {3} in {4}, {5}.\r\n{6}", FullName, Callsign, RepeaterCallsign, Frequency, City, State, ChangeDescription),
                Published = ChangeDateTime

            };

            item.AddLink(new SyndicationLink(new Uri(row["RepeaterURL"].ToString())));
            item.AddCategory(new SyndicationCategory("Technology"));
            item.AddContributor(new SyndicationPerson(String.Format("{0} ({1})", FullName, Callsign), String.Format("{0} ({1}, {2})", Email, FullName, Callsign)));

            await writer.Write(item);
        }

        xmlWriter.Flush();
    }

    return new HttpResponseMessage(HttpStatusCode.OK) 
    {
        Content = new StringContent(sw.ToString(), System.Text.Encoding.UTF8, "application/rss+xml")
    };
}

public class StringWriterWithEncoding : StringWriter
{
    private readonly Encoding _encoding;

    public StringWriterWithEncoding(Encoding encoding)
    {
        this._encoding = encoding;
    }

    public override Encoding Encoding {
        get { return _encoding; }
    }
}