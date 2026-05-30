namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<List<ColorResponse>> GetAllColors();
    }

   public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<List<ColorResponse>> GetAllColors()
        {
            var colors = await _databaseLayer.GetAllColors();
            return colors;
        }
    }
}
