namespace PaymentAPI.Models
{
    public class StatusRequest
    {
        public string Invoiceno { get; set; }
        public string Refid { get; set; }
        public string Tid { get; set; }
        public string PosResponse { get; set; }
    }
}
