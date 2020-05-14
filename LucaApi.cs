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


namespace LucaApi
{
    public class LucaApi
    {
        [FunctionName("GetFinancials")]
        public static async Task<FinancialStatement> GetFinancialsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {

            string symbol = req.Query["symbol"];
            string filing = req.Query["filing"];
            string before = req.Query["before"];
            string forcecalculation = req.Query["forcecalculation"];

            //Symbol
            if (symbol == null)
            {
                throw new Exception("Critical request error: symbol was blank.");
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
                    throw new Exception("Critical request error: value '" + filing + "' was not understood.  Options are '10k' or '10q'.");
                }
            }
            else
            {
                throw new Exception("Critical request error: parameter 'filing' was blank but is a required parameter.");
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
                }
                catch
                {
                    throw new Exception("Critical request error: unable to parse 'before' parameter of value '" + before + "' to DateTime format.");
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
                    throw new Exception("Critical request error: value '" + forcecalculation + "' was not recognized as a valid value for parameter 'forcecalculation'.  Value should either be 'true' or 'false'.");
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
                throw new Exception("Internal error: unable to establish connection to Luca storage.");
            }
            
            //Get the Financial Statement
            try
            {
                FinancialStatement fs = await lm.DownloadFinancialStatementAsync(symbol, FilingRequest, BeforeRequest, ForceCalculationRequest);
                return fs;
            }
            catch (Exception e)
            {
                throw new Exception("Critical error while downloading financial statement.  Internal error message: " + e.Message);
            }
            
            
        }

        [FunctionName("GetVersion")]
        public static string GetLucaVersionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            return LucaManager.Version.ToString();
        }

        // //Had to comment this out because the NuGet package Luca v2 hasn't published yet. Will add it back in later.
        // [FunctionName("GetLastUpdateDateTime")]
        // public static async Task<DateTime> GetLastUpdateDateTimeAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        // {
        //     LucaManager lm = LucaManager.Create();
        //     DateTime update = await lm.DownloadLatestVersionPublishedDateTimeAsync();
        //     return update;
        // }
    }

    
}
