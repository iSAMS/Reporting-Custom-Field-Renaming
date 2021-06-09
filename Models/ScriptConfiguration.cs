namespace iSAMS.Utilities.Reporting.CustomFieldRenaming.Models
{
    public class ScriptConfiguration
    {
        public string Domain { get; set; }
        public string Authority
        {
            get
            {
                return Domain + "/auth";
            }
        }
        public string RestApiClientId { get; set; }
        public string RestApiClientSecret { get; set; }
        public string TargetDirectory { get; set; }
        public string CustomFieldName { get; set; }
    }
}
