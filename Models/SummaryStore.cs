using System;
using System.Collections.Generic;
using System.Text;

namespace iSAMS.Utilities.Reporting.CustomFieldRenaming.Models
{
    public class SummaryStore
    {

        public SummaryStore()
        {
            Log = new List<string>();
            FailedRequests = 0;
            SuccessfulRequests = 0;
        }

        public List<string> Log { get; set; }
        public int FailedRequests { get; set; }
        public int SuccessfulRequests { get; set; }


        public void Add(string message, bool success)
        {
            var prefix = success ? "Success: " : "Failure: ";

            if (success)
                RequestSuccessful();
            else
                RequestFailed();

            Log.Add(prefix + message);
        }

        public void RequestFailed()
        {
            FailedRequests++;
        }

        public void RequestSuccessful()
        {
            SuccessfulRequests++;
        }
    }
}
