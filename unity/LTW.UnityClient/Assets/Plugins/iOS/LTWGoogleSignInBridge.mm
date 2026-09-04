// Native bridge from Unity to Google's GoogleSignIn-iOS SDK, used because Google archived the
// official Unity plugin (google-signin-unity) in April 2026 — see
// LTW.UnityClient/Assets/Scripts/Online/GoogleSignInIOS.cs's remarks.
//
// UNTESTED ON DEVICE as of writing — written against Google's currently documented API
// (signInWithPresentingViewController:completion: returning a GIDSignInResult, whose
// serverAuthCode property is populated automatically once GIDServerClientID is set in Info.plist —
// see developers.google.com/identity/sign-in/ios/backend-auth and .../offline-access, fetched
// while writing this). Requires:
//   - the GoogleSignIn CocoaPod linked in, via Assets/ThirdParty/GoogleSignIniOS/Editor's EDM4U
//     dependency XML;
//   - Info.plist keys GIDClientID (an iOS-type OAuth client — NOT YET CREATED, see
//     docs/PLAYFAB_SETUP.md) and GIDServerClientID (the Web-application client already configured
//     in PlayFab's Google add-on) injected by Assets/Editor/iOS/GoogleSignInPostProcessBuild.cs;
//   - a URL scheme (the iOS client's reversed client ID) also injected by that same script, for
//     the OAuth redirect back into the app.

#import <GoogleSignIn/GoogleSignIn.h>
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController();
extern "C" void UnitySendMessage(const char *className, const char *methodName, const char *message);

extern "C" {
    void _LTWGoogleSignIn_SignIn(void);
}

void _LTWGoogleSignIn_SignIn(void)
{
    UIViewController *presenter = UnityGetGLViewController();

    [GIDSignIn.sharedInstance signInWithPresentingViewController:presenter
                                                       completion:^(GIDSignInResult * _Nullable signInResult, NSError * _Nullable error) {
        if (error != nil) {
            NSString *description = error.localizedDescription ?: @"unknown Google sign-in error";
            UnitySendMessage("LTWGoogleSignInReceiver", "OnGoogleSignInFailure", description.UTF8String);
            return;
        }

        NSString *serverAuthCode = signInResult.serverAuthCode;
        if (serverAuthCode == nil || serverAuthCode.length == 0) {
            UnitySendMessage("LTWGoogleSignInReceiver", "OnGoogleSignInFailure",
                              "Google sign-in succeeded but returned no server auth code — check that GIDServerClientID is set in Info.plist.");
            return;
        }

        UnitySendMessage("LTWGoogleSignInReceiver", "OnGoogleSignInSuccess", serverAuthCode.UTF8String);
    }];
}
