using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoDB.Transaction
{
    public class Parent
    {
        [BsonConstructor]
        public Parent(
            string Id,
            string Name,
            DateTime CreatedAt,
            string[] ChildrenIds = null,
            Child[] children = null)
        {
            this.Id = Id;
            this.Name = Name;
            this.CreatedAt = CreatedAt;
            this.ChildrenIds = ChildrenIds;
            this.Children = children;
        }

        [BsonId]
        public string Id { get; private set; }

        public string Name { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public string[] ChildrenIds { get; private set; }

        public Child[] Children { get; private set; }
    }

    public class Child
    {
        [BsonConstructor]
        public Child(string Id,
                     string Name,
                     DateTime CreatedAt)
        {
            this.Id = Id;
            this.Name = Name;
            this.CreatedAt = CreatedAt;
        }

        [BsonId]
        public string Id { get; private set; }

        public string Name { get; private set; }

        public DateTime CreatedAt { get; private set; }
    }

    internal class Program
    {
        private static MongoClient _mongoClient;

        static Program()
        {
            var mongoClientSettings = new MongoClientSettings()
            {
                Server = new MongoServerAddress("localhost", 27017),
                UseTls = false,
                ReplicaSetName = "mongo-replica-set"
            };

            _mongoClient = new MongoClient(mongoClientSettings);
        }

        private static async Task Main(string[] args)
        {
            var db = _mongoClient.GetDatabase("Test");
            var parentCollection = db.GetCollection<Parent>(nameof(Parent));
            var childCollection = db.GetCollection<Child>(nameof(Child));

            #region Transaction Write 2 documents of cross collection in Atomic (Transaction)

            var case1ChildObject = new Child(
                            Id: Guid.NewGuid().ToString(),
                            Name: "Child 1",
                            CreatedAt: DateTime.UtcNow);

            var case1Object = new Parent(
                       Id: Guid.NewGuid().ToString(),
                       Name: "Parent 1",
                       CreatedAt: DateTime.UtcNow,
                       new string[] { case1ChildObject.Id });

            var session = _mongoClient.StartSession();

            session.StartTransaction();

            await parentCollection.FindOneAndReplaceAsync(
                    session,
                    filter: Builders<Parent>.Filter.Eq(x => x.Id, case1Object.Id),
                    replacement: case1Object,
                     options: new FindOneAndReplaceOptions<Parent, Parent>()
                     {
                         IsUpsert = true
                     }).ConfigureAwait(false);

            await childCollection.FindOneAndReplaceAsync(
                   session,
                   filter: Builders<Child>.Filter.Eq(x => x.Id, case1ChildObject.Id),
                   replacement: case1ChildObject,
                    options: new FindOneAndReplaceOptions<Child, Child>()
                    {
                        IsUpsert = true
                    }).ConfigureAwait(false);

            session.CommitTransaction();

            #endregion Transaction Write 2 documents of cross collection in Atomic (Transaction)

            #region Find 2 documents in spread search

            Parent savedCase1Object =
            (await parentCollection.FindAsync<Parent>(
                filter: Builders<Parent>.Filter.Eq(x => x.Id, case1Object.Id)).ConfigureAwait(false)).First();

            Child savedCase1ChildObject =
            (await childCollection.FindAsync<Child>(
              filter: Builders<Child>.Filter.Eq(x => x.Id, case1ChildObject.Id)).ConfigureAwait(false)).First();

            #endregion Find 2 documents in spread search

            #region Find 2 documents in  ($lookup)

            List<Parent> savedCase2Object =
            await parentCollection.Aggregate()
            .Match(x => x.Id == case1Object.Id)
            .Lookup(
                foreignCollectionName: nameof(Child),
                localField: nameof(Parent.ChildrenIds),
                foreignField: "_id",
                @as: nameof(Parent.Children)
            )
            .As<Parent>()
            .ToListAsync().ConfigureAwait(false);

            #endregion Find 2 documents in  ($lookup)

            #region bulkwrite


            var case3Object = new Parent(
                       Id: Guid.NewGuid().ToString(),
                       Name: "Parent 1",
                       CreatedAt: DateTime.UtcNow,
                       new string[] { case1ChildObject.Id });
            var operations = new List<WriteModel<Parent>>();

            var filter = Builders<Parent>.Filter.Eq(x => x.Id, case3Object.Id);
            operations.Add(new ReplaceOneModel<Parent>(filter, case3Object) { IsUpsert = true });

            var reslt = await parentCollection.BulkWriteAsync(operations).ConfigureAwait(false);


            #endregion
        }
    }
}