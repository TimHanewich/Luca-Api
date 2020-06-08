using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Luca;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using SecuritiesExchangeCommission.Edgar;
using Xbrl.FinancialStatement;
using Xbrl;
using System.Net;
using System.Net.Http;


namespace LucaApi
{
    public class LucaApi
    {
        [FunctionName("GetFinancials")]
        public static async Task<HttpResponseMessage> GetFinancialsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, ILogger log)
        {

            string symbol = req.Query["symbol"];
            string filing = req.Query["filing"];
            string before = req.Query["before"];
            string forcecalculation = req.Query["forcecalculation"];
            
            //Report
            log.LogInformation("Symbol: " + symbol);
            log.LogInformation("Filing: " + filing);
            log.LogInformation("Before: " + before);
            log.LogInformation("Force Calculation: " + forcecalculation);

            //Symbol
            if (symbol == null)
            {
                HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest);
                StringContent sc = new StringContent("Critical request error: symbol was blank.", System.Text.Encoding.UTF8);
                ToReturn.Content = sc;
                return ToReturn;
            }
            symbol = symbol.Trim().ToLower();

            //Filing
            FilingWithXbrlDocument FilingRequest;
            if (filing != null)
            {
                if (filing.ToLower() == "10q")
                {
                    FilingRequest = FilingWithXbrlDocument.Filing10q;
                }
                else if (filing.ToLower() == "10k")
                {
                    FilingRequest = FilingWithXbrlDocument.Filing10k;
                }
                else
                {
                    HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    StringContent sc = new StringContent("Fatal request error: value '" + filing + "' was not understood.  Options are '10k' or '10q'.", System.Text.Encoding.UTF8);
                    ToReturn.Content = sc;
                    return ToReturn;
                }
            }
            else
            {
                HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest);
                StringContent sc = new StringContent("Fatal request error: parameter 'filing' was blank but is a required parameter.", System.Text.Encoding.UTF8);
                ToReturn.Content = sc;
                return ToReturn;
            }
            

            //Before
            //Format for the before request: MMDDYYYY
            DateTime BeforeRequest = DateTime.UtcNow;
            if (before != null)
            {
                try
                {
                    string monthstr = before.Substring(0, 2);
                    string daystr = before.Substring(2, 2);
                    string yearstr = before.Substring(4, 4);
                    BeforeRequest = new DateTime(Convert.ToInt32(yearstr), Convert.ToInt32(monthstr), Convert.ToInt32(daystr));
                    log.LogInformation("Before date used for EDGAR query: " + BeforeRequest.ToShortDateString());
                }
                catch
                {
                    HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    StringContent sc = new StringContent("Fatal request error: unable to parse 'before' parameter of value '" + before + "' to DateTime format.", System.Text.Encoding.UTF8);
                    ToReturn.Content = sc;
                    return ToReturn;
                }
            }


            //Force Calculation
            bool ForceCalculationRequest = false;
            if (forcecalculation != null)
            {
                if (forcecalculation.ToLower() == "true")
                {
                    ForceCalculationRequest = true;
                }
                else if (forcecalculation.ToLower() == "false")
                {
                    ForceCalculationRequest = false;
                }
                else
                {
                    HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest);
                    StringContent sc = new StringContent("Critical request error: value '" + forcecalculation + "' was not recognized as a valid value for parameter 'forcecalculation'.  Value should either be 'true' or 'false'.", System.Text.Encoding.UTF8);
                    ToReturn.Content = sc;
                    return ToReturn;
                }
            }

            //Create Luca Manager Client
            LucaManager lm;
            try
            {
                lm = LucaManager.Create();
            }
            catch
            {
                HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                StringContent sc = new StringContent("Internal error: unable to establish connection to Luca storage.", System.Text.Encoding.UTF8);
                ToReturn.Content = sc;
                return ToReturn;
            }
            
            //Get the Financial Statement
            try
            {
                log.LogInformation("Downloading financial statement...");
                FinancialStatement fs = await lm.DownloadFinancialStatementAsync(symbol, FilingRequest, BeforeRequest, ForceCalculationRequest);
                string asJson = JsonConvert.SerializeObject(fs);
                
                HttpResponseMessage hrm = new HttpResponseMessage(HttpStatusCode.OK);
                StringContent sc = new StringContent(asJson, System.Text.Encoding.UTF8, "application/json");
                hrm.Content = sc;
                return hrm;
            }
            catch (Exception e)
            {
                HttpResponseMessage ToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                StringContent sc = new StringContent("Fatal error while downloading financial statement.  Internal error message: " + e.Message, System.Text.Encoding.UTF8);
                ToReturn.Content = sc;
                return ToReturn;
            }
            
            
        }

        [FunctionName("GetVersion")]
        public static string GetLucaVersionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return LucaManager.Version.ToString();
        }

        [FunctionName("GetLastUpdateDateTime")]
        public static async Task<DateTime> GetLastUpdateDateTimeAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            LucaManager lm = LucaManager.Create();
            DateTime update = await lm.DownloadLatestVersionPublishedDateTimeAsync();
            return update;
        }
    }

    
}
