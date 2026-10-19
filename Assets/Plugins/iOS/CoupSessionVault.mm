#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>
static NSMutableDictionary* CoupKey() {
    return [@{(__bridge id)kSecClass:(__bridge id)kSecClassGenericPassword,
              (__bridge id)kSecAttrService:@"coup.refresh.v1", (__bridge id)kSecAttrAccount:@"current"} mutableCopy];
}
extern "C" char* CoupCredentialRead() {
    NSMutableDictionary* query=CoupKey(); query[(__bridge id)kSecReturnData]=@YES;
    CFTypeRef result=NULL; OSStatus status=SecItemCopyMatching((__bridge CFDictionaryRef)query,&result);
    if(status==errSecItemNotFound) return NULL;
    if(status!=errSecSuccess) return NULL;
    NSData* data=(__bridge_transfer NSData*)result;
    NSString* text=[[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    return text ? strdup(text.UTF8String) : NULL;
}
extern "C" bool CoupCredentialWrite(const char* raw) {
    NSMutableDictionary* query=CoupKey();
    if(!raw || !raw[0]) { OSStatus status=SecItemDelete((__bridge CFDictionaryRef)query); return status==errSecSuccess || status==errSecItemNotFound; }
    NSData* data=[[NSString stringWithUTF8String:raw] dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary* attributes=@{(__bridge id)kSecValueData:data};
    OSStatus status=SecItemUpdate((__bridge CFDictionaryRef)query,(__bridge CFDictionaryRef)attributes);
    if(status==errSecItemNotFound) { [query addEntriesFromDictionary:attributes]; query[(__bridge id)kSecAttrAccessible]=(__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly; status=SecItemAdd((__bridge CFDictionaryRef)query,NULL); }
    return status==errSecSuccess;
}
extern "C" void CoupCredentialFree(char* value) { free(value); }
