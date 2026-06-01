namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<List<ColorResponse>> GetAllColors();
        Task<ColorResponse> CreateColor(ColorResponse color);
        Task<ColorResponse> UpdateColor(int id, ColorResponse color);
        Task<ColorResponse> DeleteColor(int id);
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

        public async Task<ColorResponse> UpdateColor(int id, ColorResponse color)
        {
            var updatedColor = await _databaseLayer.UpdateColor(id, color);
            return updatedColor;
        }
        public async Task<ColorResponse> DeleteColor(int id)
        {
            var deletedColor = await _databaseLayer.DeleteColor(id);
            return deletedColor;

        }
    }
}
