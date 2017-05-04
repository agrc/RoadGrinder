namespace RoadGrinder.models
{
    public class IndexFieldValue : IndexFieldMap
    {
        public object Value { get; set; }

        public IndexFieldValue(int index, string field, object value) : base(index, field)
        {
            Value = value;
        }
    }
}