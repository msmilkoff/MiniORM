namespace MiniORM.Attributes
{
    using System;

    [AttributeUsage(AttributeTargets.Field)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; set; }
    }
}