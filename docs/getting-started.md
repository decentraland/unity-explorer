# Getting Started

The unity project can be found [here](https://github.com/decentraland/unity-explorer/tree/main/Explorer). Currently there are two scenes available.

## Dynamic Scene Loader

Choose a realm, and load each scene dynamically.

Dynamic Scene Loader is an entry point for a "production-ready" flow of the realms loading. It's responsible for calling root registration of containers and launching a realm.

As we move forward with the development it will become a place to start an asynchronous "Loading Screen" (or something similar).

## Static Scene Loader

Select from an individual scene.

Static Scene Loader can be used to debug and test individual scenes without connection to Realm. It contains a subset of containers and systems and injects the logic required for running an individual scene only.
