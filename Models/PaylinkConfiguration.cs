namespace PaymentAPI.Models
{
    public class PaylinkConfiguration
    {
        public int PaylinkConfigId { get; set; }
        public int CompanyId { get; set; }
        public int LocationId { get; set; }
        public int ProviderId { get; set; }
        public string PosMachineSno { get; set; }
        public string PosTerminalId { get; set; }
        public string PosMID { get; set; }
        public string PosSaltkey { get; set; }
        public string PosSecretKey { get; set; }
        public string PosMerchantPoscode { get; set; }
        public string PosUserid { get; set; }
        public string PosPassword { get; set; }
        public string PosInitiateurl { get; set; }
        public string PosStatusurl { get; set; }
        public string PosInitiatecancelUrl { get; set; }
        public string PosRefundurl { get; set; }
        public string PosVoidurl { get; set; }
        public string TransactionNumber { get; set; }
        public string transaction_number_min_length { get; set; }
        public int? CreateBy { get; set; }
        public char? RecordStatus { get; set; }
    }

}
