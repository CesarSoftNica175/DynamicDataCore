namespace DynamicDataCore.Abstractions
{
    public interface IBaseGenericServiceFactory
    {

        IBaseGenericService<T> Create<T>(string schemaName) where T : class;

    }
}
