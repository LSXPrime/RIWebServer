using System.Collections;
using System.Data;
using System.Reflection;
using RIWebServer.Attributes.ORM;

namespace RIWebServer.ORM;

public class DbSet<TEntity>(DbContext dbContext)
    where TEntity : class, new()
{
    private readonly string _tableName = typeof(TEntity).Name;

    /// <summary>
    /// Retrieves an entity of type TEntity by its ID from the database.
    /// </summary>
    /// <param name="id">The ID of the entity to retrieve.</param>
    /// <returns>The entity with the specified ID, or null if not found.</returns>
    public TEntity? GetById(int id)
    {
        using var connection = dbContext.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_tableName} WHERE Id = @Id";
        var param = command.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = id;
        command.Parameters.Add(param);

        using var reader = command.ExecuteReader();
        return MapReaderToList<TEntity>(reader).FirstOrDefault();
    }

    /// <summary>
    /// Retrieves all entities of type TEntity from the database.
    /// </summary>
    /// <returns>A list of entities of type TEntity.</returns>
    public List<TEntity> GetAll()
    {
        using var connection = dbContext.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_tableName}";

        using var reader = command.ExecuteReader();
        return MapReaderToList<TEntity>(reader).ToList();
    }

    /// <summary>
    /// Retrieves all entities of type TEntity from the database along with their related entities
    /// as specified by foreign key attributes.
    /// </summary>
    /// <returns>A list of entities of type TEntity with their related entities populated.</returns>
    public List<TEntity> GetAllWithRelated()
    {
        var entities = GetAll();
        var navigationProperties = typeof(TEntity).GetProperties()
            .Where(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null && p.CanWrite);

        foreach (var navProperty in navigationProperties)
        {
            var foreignKeyProperty = navProperty.Name;
            var relatedTable = navProperty.GetCustomAttribute<ForeignKeyAttribute>()!.RelatedTable;
            
            var relatedIds = entities.Select(e => (int)e.GetType().GetProperty(foreignKeyProperty)!.GetValue(e)!)
                .Distinct().ToList();
            
            var relatedEntities = GetRelatedEntities(relatedTable, relatedIds);

            foreach (var entity in entities)
            {
                var relatedId = (int)entity.GetType().GetProperty(foreignKeyProperty)!.GetValue(entity)!;
                var mainEntity =
                    relatedEntities.FirstOrDefault(re => dbContext.GetPrimaryKeyValue(re).Equals(relatedId));
                var relatedEntity = mainEntity?.GetType().GetProperties()
                    .FirstOrDefault(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null)
                    ?.GetValue(mainEntity)!;

                navProperty.SetValue(entity, relatedEntity);
            }
        }
        return entities;
    }

    /// <summary>
    /// Retrieves related entities from the specified table based on the provided list of related IDs.
    /// </summary>
    /// <param name="relatedTable">The name of the related table.</param>
    /// <param name="relatedIds">The list of related IDs.</param>
    /// <returns>A list of related entities.</returns>
    private List<object> GetRelatedEntities(string relatedTable, List<int> relatedIds)
    {
        try
        {
            var relatedDbSetProperty = dbContext.GetType().GetProperty(relatedTable + "s")!;
            var relatedDbSet = relatedDbSetProperty.GetValue(dbContext);

            var getAllMethod = relatedDbSetProperty.PropertyType.GetMethod("GetAll");
            var relatedEntities = (IList)getAllMethod!.Invoke(relatedDbSet, null)!;
            
            return relatedEntities.Cast<object>()
                .Where(re => relatedIds.Contains((int)dbContext.GetPrimaryKeyValue(re)))
                .ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Adds an entity to the database context.
    /// </summary>
    /// <param name="entity">The entity to be added.</param>
    public void Add(TEntity entity)
    {
        dbContext.AddEntity(entity);
    }

    /// <summary>
    /// Updates the specified entity in the database context.
    /// </summary>
    /// <param name="entity">The entity to be updated.</param>
    public void Update(TEntity entity)
    {
        dbContext.UpdateEntity(entity);
    }

    /// <summary>
    /// Deletes an entity from the database context by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to be deleted.</param>
    public void Delete(int id)
    {
        var entity = GetById(id);
        if (entity != null)
        {
            dbContext.DeleteEntity(entity);
        }
    }

    /// <summary>
    /// Maps a data reader to a list of objects of type TMap.
    /// </summary>
    /// <typeparam name="TMap">The type of objects to map to.</typeparam>
    /// <param name="reader">The data reader to read from.</param>
    /// <returns>An enumerable of objects of type TMap.</returns>
    private IEnumerable<TMap> MapReaderToList<TMap>(IDataReader reader) where TMap : class, new()
    {
        var entityType = typeof(TMap);
        var properties = entityType.GetProperties().Where(p => p.CanWrite).ToArray();

        while (reader.Read())
        {
            var entity = new TMap();
            foreach (var property in properties)
            {
                var columnName = property.Name;
                var ordinal = reader.GetOrdinal(columnName);

                if (!reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);

                    if (property.PropertyType == typeof(DateTime) && value is string dateTimeString)
                    {
                        value = DateTime.Parse(dateTimeString);
                    }

                    if (property.PropertyType == typeof(int) && value is long longValue)
                    {
                        value = (int)longValue;
                    }

                    property.SetValue(entity, value);
                }
            }

            yield return entity;
        }
    }
}