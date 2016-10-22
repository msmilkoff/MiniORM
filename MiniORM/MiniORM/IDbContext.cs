namespace MiniORM
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines the operations that can be performed with the database. 
    /// </summary>
    public interface IDbContext
    {
        /// <summary>
        /// Insert or Update entity depending if it's attached to the context.
        /// </summary>
        bool Persist(object entity);

        /// <summary>
        /// Gets entity object by given id.
        /// </summary>
        T FindById<T>(int id);

        /// <summary>
        /// Returns a collection of all entity objects.
        /// </summary>
        IEnumerable<T> FindAll<T>();

        /// <summary>
        /// Returns a collection of all entity objects, matching the criteria given in "where".
        /// </summary>
        IEnumerable<T> FindaAll<T>(string where);

        /// <summary>
        /// Returns the first entity object.
        /// </summary>
        T FindFirst<T>();

        /// <summary>
        /// Returns the first entity object, matching the criteria given in "where".
        /// </summary>
        T FindFirst<T>(string where);
    }
}