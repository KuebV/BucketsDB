using BucketsDB.Property;

namespace BucketsDB;

public class BucketProperty
{
    [Property("How many items can be placed into the specified bucket?")]
    public int MaxItems { get; set; }
    
    [Property("If the current bucket is filled, then the new item will be placed into a new bucket")]
    public bool ManageOverflow { get; set; }
    
    [Property("Current Overflow Bucket")]
    public string? CurrentOverflowBucket { get; set; }

    [Property("Current Home Bucket")] 
    public string HomeBucket { get; set; }

    public long CreationDate { get; set; }
    
    public bool IgnoreTypeCheck { get; set; }

    public bool CurrentlyOverflowing { get; set; }
    
}