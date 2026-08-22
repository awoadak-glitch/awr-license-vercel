#define _GNU_SOURCE
#include <jni.h>
#include <link.h>
#include <stdint.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>

static uintptr_t native_base = 0;

static int find_cb(struct dl_phdr_info *info, size_t size, void *data) {
    (void)size; (void)data;
    if (info && info->dlpi_name && strstr(info->dlpi_name, "libNative.so")) {
        native_base = (uintptr_t)info->dlpi_addr;
        return 1;
    }
    return 0;
}

static int patch_one(uintptr_t addr, int enabled) {
    long ps = sysconf(_SC_PAGESIZE);
    if (ps <= 0) ps = 4096;
    uintptr_t page = addr & ~((uintptr_t)ps - 1u);
    if (mprotect((void*)page, (size_t)ps, PROT_READ | PROT_WRITE) != 0) return 0;

    volatile uint32_t *p = (volatile uint32_t*)addr;
    p[0] = enabled ? 0x52800020u : 0x52800000u; /* mov w0,#1 / mov w0,#0 */
    p[1] = 0xD65F03C0u;                         /* ret */
    __builtin___clear_cache((char*)addr, (char*)(addr + 8));

    if (mprotect((void*)page, (size_t)ps, PROT_READ | PROT_EXEC) != 0) return 0;
    return 1;
}

JNIEXPORT jboolean JNICALL
Java_com_awr_license_AwrLicenseInitializationContentProviderForHiTV2026_nativeSetVipEnabled(
        JNIEnv *env, jclass clazz, jboolean enabled) {
    (void)env; (void)clazz;
    native_base = 0;
    dl_iterate_phdr(find_cb, 0);
    if (!native_base) return JNI_FALSE;

    /* UserState JNI wrappers in libNative.so (HiTV 3.1.2 / versionCode 81). */
    static const uintptr_t offsets[] = {
        0x62794, /* isMemberValid */
        0x631A4, /* vipEnableAd */
        0x63254, /* vipEnableDownloadFast */
        0x63304, /* vipEnableDownloadHd */
        0x633B4, /* vipEnableDownloadParallel */
        0x63464, /* vipEnablePaidVideo */
        0x63514, /* vipEnablePlayHd */
        0x635C4, /* vipEnableProjection */
        0x63674, /* vipEnableTV */
        0x63724  /* vipEnableTogetherVoice */
    };

    int value = enabled ? 1 : 0;
    for (unsigned i = 0; i < sizeof(offsets)/sizeof(offsets[0]); ++i) {
        if (!patch_one(native_base + offsets[i], value)) return JNI_FALSE;
    }
    return JNI_TRUE;
}
