# Network Synchronization

## `OnUserEnter` & `OnUserLeave`, Entities Availability

### Our User

All components (`PBIdentityData`, `PBAvatarEquippedData`, `PBAvatarBase`) related to the host identity are present on the scene on start-up.

The original necessity of it is described [here](https://github.com/decentraland/unity-explorer/issues/1086).

Thus, these components are present throughout the whole lifecycle of the scene.

> **Warning:** It may lead to unexpected behaviour from the JS perspective: Host Components will be always present, no matter if the user is inside the scene or outside!

### Remote Entities

In turn Remote Entities function completely differently:
- If the scene is not the current scene (the host is not standing there) remote entities can't be present on the scene (in CRDT state)
   - It might be confusing because visually they could be there:
     - In worlds as the same LiveKit room is used for the whole world
     - In Genesis City as remote entities are retrieved from an Island which is not related to the current room
- When the host enters the scene all remote participants standing within the bounds of that scene are propagated as they were just entered that scene:
   - Corresponding `PBIdentityData`, `PBAvatarEquippedData`, `PBAvatarBase` added
   - As a result the initial state of the scene is broadcasted through the `Scene` LiveKit room
- When the host is within scene boundaries and other players enter and leave the scene it works as expected:
   - When the remote participant enters the corresponding components are added
   - When they leave - components are removed

> **Warning:** Entities themselves are never deleted explicitly: their range for remote participants is reserved.

### Why It Works This Way

- `Scene Synchronization` is driven through `Scene` LiveKit room
- `Host` can only have one connection to the `Scene` room at a time so the client connects to the room of the scene the host is currently located on
- So even if remote participants stand on non-current scenes we can't synchronize scene state that is caused by users' actions
- Apart from `Scene` LiveKit room there is an `Island` room
   - We don't use `Island` for scene synchronization as it leads to broadcasting of irrelevant data and it can consume all bandwidth
   - However, we use it in `unity-renderer` as there is no concept of `Scene` rooms. It can lead to additional confusion if two versions are compared face-to-face
   - But we use `Island` to display other participants (as otherwise the world would be empty and users would pop-up out of nowhere when the host enters the scene)
