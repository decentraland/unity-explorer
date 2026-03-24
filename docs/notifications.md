# Notifications

## Introduction

In the DCL ecosystem we have some services that provide the user with a series of notifications for different purposes such as Events starting, new items received, DAO proposal voting status and others.
The notifications were generally visible in the DCL website once a user logged in and now, in Explorer Alpha, are also integrated in the client.

## Notification Bus Controller

Because in the client a series of actions can be called based on the type of notification we receive or interact with, a controller has been created to allow easy injection in any needed class.
The INotificationsBusController
```csharp
    public interface INotificationsBusController
    {
        void AddNotification(INotification notification);
        void ClickNotification(NotificationType notificationType, params object[] parameters);
        void SubscribeToNotificationTypeClick(NotificationType desiredType, NotificationsBusController.NotificationClickedDelegate listener);
        void SubscribeToNotificationTypeReceived(NotificationType desiredType, NotificationsBusController.NotificationReceivedDelegate listener);
    }
```
allows us to subscribe to specific notification types and execute a callback.

We can also subscribe to a click of specific notification types with the `SubscribeToNotificationTypeClick` function.

For example, when we click on a notification of a new item received, the client currently opens the backpack.


The AddNotification function is also exposed in order to allow the creation of local notifications that will take advantage of the structure.

For example, once we set a parcel as destination and reach it, an internal notification is displayed on top of the screen.

## Retrieving Remote Notifications

In order to retrieve notifications from the BE the current approach is just via HTTP polling at the following URL: https://notifications.decentraland.org/notifications (.zone if we want to test in the dev environment).

Some query params are also supported to filter the notifications (more info [here](https://github.com/decentraland/notifications-workers/tree/main?tab=readme-ov-file#get-notifications))
You can see the usage of this in the NotificationsRequestController.cs class.

## Adding Support for a New Remote Notification

In case a new notification type is defined in the BE, to add the client counterpart the following actions have to be made:
* Add a new NotificationType with the same name as the BE one (available [here](https://github.com/decentraland/schemas/blob/main/src/platform/notifications/notifications.ts))
* Add a new class that extends NotificationBase in the NotificationTypes folder and define the metadata with the proper json naming
* Add the class instantiation in the NotificationJsonDtoConverter

And then you can subscribe to this new notification type wherever needed.

## Testing Remote Notifications Locally

In order to test notifications we need to point to the .zone notification retrieval endpoint and then send some requests to the notification processor at https://notifications-processor.decentraland.zone/notifications

For example, if we want to test a reward assignment notification we can send a POST to https://notifications-processor.decentraland.zone/notifications with the following body:
```json
[
    {
        "type": "reward_assignment",
        "address": "{receiverAddress}",
        "eventKey": "{uniqueId}",
        "metadata": {
            "title": "New item received",
            "tokenName": "DCLGX24 Pants",
            "tokenImage": "https://peer.decentraland.org/lambdas/collections/contents/urn:decentraland:matic:collections-v2:0x0ae365f8acc27f2c95fc7d60cf49a74f3af21573:1/thumbnail",
            "description": "This DCLGX24 Pants is already in your backpack",
            "tokenRarity": "uncommon"
        },
        "timestamp": {timestamp}
    }
]
```
In addition we need to provide an auth bearer header that won't be provided here for security reasons, it can be asked to the BE team.
