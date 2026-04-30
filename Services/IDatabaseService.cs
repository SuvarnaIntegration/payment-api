namespace PaymentAPI.Services
{
    public interface IDatabaseService
    {
        Task InsertPaylinkCompanyAsync(string companyCd, string companyName, int createBy, char recordStatus);
    }
}
