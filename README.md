# Minimal Suave Host for Azure Service Fabric 

[![Build status](https://ci.appveyor.com/api/projects/status/vtgo8pk4gf49ec4f/branch/master?svg=true)](https://ci.appveyor.com/project/Krzysztof-Cieslak/servicefabricsuave/branch/master)

Based ok https://github.com/isaacabraham/ServiceFabricFsDemo. 

Project shows how to host Suave application inside of Azure Service Fabric stateless service. It also demonstrates build, testing and relesing process which can be used for similar applications.

## Build process

The automated build process contains:

1. Restore dependencies using Paket
1. Clean artifacts from previous build
1. Update assembly infos and manifest files with version from RELEASE_NOTES.md file (this probably should be done differently - in Service Fabric you can have seperate versioning for applications and services being part of it)
1. Build application and tests projects
1. Run unit tests (plain business logic tests - not testing anything Suave or Service Fabric releated)
1. Package application
1. Start local development Service Fabric Cluster (and remove previously deployed versions of application)
1. Deploy application to local cluster.
1. Run integration tests (testing Suave and Service Fabric hosting - making normal HTTP requests to locally deployed application)
1. Push changes to GitHub, upload packaged application, create tag and release

## Build server

Project is configured to use appveyor build server for testing any changes to master branch and any PRs. Unfortunatlly right now it's only running unit tests - I haven't managed to start local development cluster on appveyor build server so Integration tests can't be run.
