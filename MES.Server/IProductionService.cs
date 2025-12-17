public interface IProductionService
{
    void CreateProductionForLatestOrder();
    void UpdateLatestProductionEndDate();
    void EnforceProductionRetention();
    void IncrementLatestProductionGoodQuantity();
    void IncrementLatestProductionBadQuantity();
    void InsertVisionUpperResult(string result);
    void InsertVisionLowerResult(string result);
}
