## Next Chat - A chat service 

- Demo available at: https://goofy-noyce-e110ea.netlify.app/
- Demo user: next@gmail.com, password: jk12jl14, but you can also create new user with any valid but not necessary real email
- [Demo client repository](https://github.com/fuksi/next-chat-client)

## Description

This repo contains a service fabric application for a chat service back end.
The application contains a Stateless API which is exposed externally via Https/Wss, and two actor services as the persistent layer. More on the approach in `docs/Approach.md`

## Set up project locally
- .NET CORE SDK 3.1, VS 2019, Service Fabric SDK are required
- The solution should run out of the box, using a local service fabric cluster

## CI/CD
- CI/CD was done with Azure DevOps
- Pipeline template is available at `azure-pipelines.yml`. Remember to prepare variables used in the template
- Pipeline can also be created with Pipeline Classic UI using Azure Service Fabric, but some modifications needed. Please refer to steps in `azure-pipelines.yml`
- Unfortunately I can't publish Release json here since it contains tenant information
- All CI/CD was done following [this MSFT guide](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-tutorial-deploy-app-with-cicd-vsts)





