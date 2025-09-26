#import <AVFoundation/AVFoundation.h>

// shows system popup if not decided yet
extern "C" void RequestMicrophonePermission()
{
    [AVCaptureDevice requestAccessForMediaType:AVMediaTypeAudio
                             completionHandler:^(BOOL granted) {
        // callback can be used in the future
    }];
}

extern "C" int CurrentMicrophonePermission()
{
    AVAuthorizationStatus status = [AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeAudio];
    switch (status) {
        case AVAuthorizationStatusNotDetermined: return 0; // NotRequestedYet
        case AVAuthorizationStatusAuthorized:    return 1; // Granted
        case AVAuthorizationStatusDenied:        return 2; // Rejected
        case AVAuthorizationStatusRestricted:    return 3; // treat restricted as Rejected
    }
    return 4;
}
