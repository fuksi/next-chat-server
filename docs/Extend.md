## How could you extend your solution in the future?

If time permits, I'll extend the solution in these priorities (with smallest beging the highest):

1. **e2e testing**: the application doesn't have complicated business logic, and it's mostly about wiring layers, so e2e testing will provide most value here in term of testing coverage. A TDD (integration) attempt was made, but I was not familiar with constructing the cluster locally for test (as you would normally do with integration tests for Web API). Still, e2e testing can be made now as a remote cluster is available

2. **scale signalr**: Core SignalR is highly performant but it cannot scale out automatically. This can be solved with Azure SignalR service.

3. **caching in webapi**: group metadata can be cached since iteration over all actors/paritions can be expensive

4. **better actor interface/state storage**: metadata can be grouped, less unnecessary I/O

5. **isolated testing**: as the approach become more stable, logic e.g. in Hub and its service can be tested in an isolated manner. This will help a lot with regression in the future