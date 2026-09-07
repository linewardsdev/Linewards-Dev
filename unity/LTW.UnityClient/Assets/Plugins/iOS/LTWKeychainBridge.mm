// Native Keychain bridge, replacing PlayerPrefs for the PlayFab session ticket/id — see
// PlayFabSession.cs's own remarks. Synchronous and local (no UI, no network), unlike
// LTWGoogleSignInBridge.mm's async sign-in flow, so this needs none of that file's
// UnitySendMessage/receiver plumbing: every function here returns its result directly.
//
// UNTESTED ON DEVICE as of writing — same caveat as LTWGoogleSignInBridge.mm and for the same
// reason (this repo's Xcode export lives in a different location than this Unity project, so a
// real device/simulator run has not exercised this). Written directly against Apple's own
// Keychain Services API (Security/SecItem.h), which is stable and has not changed shape in years,
// but the ABSENCE of a live run means this is a best-effort implementation, not a verified one —
// see docs/SECURITY_AUDIT_2026-09-05.md's M-C2.
//
// kSecAttrAccessibleWhenUnlockedThisDeviceOnly (per M-C2's own fix note) means: unreadable while
// the device is locked, and — critically for a bearer credential — EXCLUDED from encrypted
// iCloud Keychain backups/restores to a different device. kSecAttrSynchronizable is simply never
// set (its default is false), which is what keeps this out of iCloud Keychain sync in the first
// place; the "ThisDeviceOnly" accessible variant is redundant insurance against ever setting it
// by mistake later.

#import <Security/Security.h>
#import <Foundation/Foundation.h>

extern "C" {
    int _LTWKeychain_Set(const char *account, const char *value);
    int _LTWKeychain_Get(const char *account, char *buffer, int bufferSize);
    int _LTWKeychain_Delete(const char *account);
}

// One fixed service string namespaces every item this bridge ever stores within the app's own
// keychain — the bundle id, not a literal, so two different LTW builds (dev/prod bundle ids) on
// the same device never see each other's stored items.
static NSString *LTWKeychainServiceName(void)
{
    return [[NSBundle mainBundle] bundleIdentifier] ?: @"com.ltw.unityclient";
}

static NSMutableDictionary *LTWKeychainQuery(NSString *account)
{
    return [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: LTWKeychainServiceName(),
        (__bridge id)kSecAttrAccount: account,
    } mutableCopy];
}

int _LTWKeychain_Set(const char *account, const char *value)
{
    if (account == NULL || value == NULL) {
        return 0;
    }

    NSString *accountString = [NSString stringWithUTF8String:account];
    NSData *valueData = [NSString stringWithUTF8String:value] != nil
        ? [[NSString stringWithUTF8String:value] dataUsingEncoding:NSUTF8StringEncoding]
        : nil;
    if (accountString == nil || valueData == nil) {
        return 0;
    }

    NSMutableDictionary *query = LTWKeychainQuery(accountString);

    // Upsert: try add first (the common case — a fresh sign-in), fall back to update on the one
    // case that means "already there" (a restored session being refreshed). Two separate calls
    // rather than delete-then-add: a failed update leaves the OLD value in place instead of
    // leaving the account with nothing stored at all.
    NSMutableDictionary *addQuery = [query mutableCopy];
    addQuery[(__bridge id)kSecValueData] = valueData;
    addQuery[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly;

    OSStatus status = SecItemAdd((__bridge CFDictionaryRef)addQuery, NULL);
    if (status == errSecDuplicateItem) {
        NSDictionary *update = @{ (__bridge id)kSecValueData: valueData };
        status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
    }

    return status == errSecSuccess ? 1 : 0;
}

int _LTWKeychain_Get(const char *account, char *buffer, int bufferSize)
{
    if (account == NULL || buffer == NULL || bufferSize <= 0) {
        return -1;
    }

    NSString *accountString = [NSString stringWithUTF8String:account];
    if (accountString == nil) {
        return -1;
    }

    NSMutableDictionary *query = LTWKeychainQuery(accountString);
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;

    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
    if (status != errSecSuccess || result == NULL) {
        return -1;
    }

    NSData *data = (__bridge_transfer NSData *)result;
    // Truncated, not failed, on an oversized value: a session ticket that somehow grew past the
    // caller's buffer should surface as "corrupt/too long" to PlayFabSession's own restore logic
    // (which then treats it like any other invalid stored value), not crash this call.
    NSUInteger length = MIN((NSUInteger)data.length, (NSUInteger)(bufferSize - 1));
    memcpy(buffer, data.bytes, length);
    buffer[length] = '\0';
    return (int)length;
}

int _LTWKeychain_Delete(const char *account)
{
    if (account == NULL) {
        return 0;
    }

    NSString *accountString = [NSString stringWithUTF8String:account];
    if (accountString == nil) {
        return 0;
    }

    OSStatus status = SecItemDelete((__bridge CFDictionaryRef)LTWKeychainQuery(accountString));
    // errSecItemNotFound counts as success here — Delete's contract is "this account is not
    // present afterward", which is already true if it was never there.
    return (status == errSecSuccess || status == errSecItemNotFound) ? 1 : 0;
}
