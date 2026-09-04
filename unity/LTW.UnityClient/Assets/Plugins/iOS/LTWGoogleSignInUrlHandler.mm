// Forwards the OAuth redirect URL to GIDSignIn so an in-progress Google sign-in can actually
// complete. Without this, GIDSignIn.sharedInstance's completion handler eventually fires with
// "The user canceled the sign-in flow" even when the user DID complete the Google sign-in screen —
// because from GIDSignIn's perspective, it never received the redirect that says the flow finished.
//
// UnityAppController.mm already implements application:openURL:options: and posts kUnityOnOpenURL
// via AppController_SendNotificationWithArg to any registered AppDelegateListener — this is
// Unity's own documented extension point for exactly this situation (see
// Classes/PluginBase/AppDelegateListener.h's doc comments in the exported Xcode project), so this
// registers a listener instead of swizzling or modifying Unity's generated code, which gets
// regenerated on every export anyway.

#import <GoogleSignIn/GoogleSignIn.h>
#import "AppDelegateListener.h"

@interface LTWGoogleSignInUrlHandler : NSObject<AppDelegateListener>
@end

@implementation LTWGoogleSignInUrlHandler

- (void)onOpenURL:(NSNotification *)notification
{
    NSURL *url = notification.userInfo[@"url"];
    if (url != nil) {
        [GIDSignIn.sharedInstance handleURL:url];
    }
}

@end

__attribute__((constructor))
static void LTWGoogleSignInUrlHandler_Register(void)
{
    static LTWGoogleSignInUrlHandler *handler;
    handler = [[LTWGoogleSignInUrlHandler alloc] init];
    UnityRegisterAppDelegateListener(handler);
}
