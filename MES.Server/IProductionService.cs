public interface IProductionService
{
    void CreateProductionForLatestOrder();
    void UpdateLatestProductionEndDate();
    void EnforceProductionRetention();
}
