#import <AVFoundation/AVFoundation.h>

// shows system popup if not decided yet
extern "C" void RequestMicrophonePermission()
{
    [[AVAudioSession sharedInstance] requestRecordPermission:^(BOOL granted) {
        // callback can be used in the future
    }];
}

extern "C" int CurrentMicrophonePermission()
{
    AVAudioSessionRecordPermission permission = [[AVAudioSession sharedInstance] recordPermission];
    switch (permission) {
        case AVAudioSessionRecordPermissionUndetermined:
            return 0; // NotRequestedYet
        case AVAudioSessionRecordPermissionGranted:
            return 1; // Granted
        case AVAudioSessionRecordPermissionDenied:
            return 2; // Rejected
    }
    return 2; // Rejected by default
}
