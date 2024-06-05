using System.Text.Json;
using BucketsDB;

public class Person 
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public double Balance { get; set; }

}

public class Program
{
    private static Random _random;
    private static Bucket<Person> bucket;
    public static void Main(string[] args)
    {
        string[] firstNames = File.ReadAllLines("first-names.txt");
        string[] lastNames = File.ReadAllLines("last-names.txt");

        _random = new Random();
        
        bucket = new Bucket<Person>("people", 16000);
        bucket.Init();

        /*for (int i = 0; i < 1600000; i++)
        {
            Person randPerson = new Person
            {
                FirstName = firstNames[_random.Next(0, firstNames.Length)],
                LastName = lastNames[_random.Next(0, lastNames.Length)],
                Age = _random.Next(18, 55),
                Balance = _random.Next(0, 10000)
            };

            bucket.Add(randPerson);
        }*/
        
        bucket.Close();

        TimeAction();
    }

    private static void TimeAction()
    {
        long t1 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        //List<Person> peopleList = bucket.GetItems();
        Person p1 = bucket.FindByKeyValue("Age", 22);
        long t2 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        
        Console.WriteLine($"{t2-t1}ms");

    }
}
