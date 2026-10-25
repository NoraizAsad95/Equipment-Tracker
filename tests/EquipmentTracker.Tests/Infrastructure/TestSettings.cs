// Every test class creates its own LocalDB database. Running all of them at once overloads LocalDB
// and connections time out, so only a few run at the same time.
[assembly: CollectionBehavior(MaxParallelThreads = 3)]
