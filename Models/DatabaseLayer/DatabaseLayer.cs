namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {

    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        private readonly IConfiguration _configuration;
        private readonly string DbConnection;
        public DataBaseLayer(IConfiguration configuration)
        {
            this._configuration = configuration;
            this.DbConnection = this._configuration.GetConnectionString("AppDbContextConnection");
        }
    }
}
