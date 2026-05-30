namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<List<ColorResponse>> GetAllColors();
        Task<ColorResponse> CreateColor(ColorResponse color);
    }

   public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<List<ColorResponse>> GetAllColors()
        {
            var colors = await _databaseLayer.GetAllColors();
            return colors;
        }

        public async Task<ColorResponse> CreateColor(ColorResponse color)
        {
            var createdColor = await _databaseLayer.CreateColor(color);
            return createdColor;
        }
    }
}
