#include <jni.h>

static volatile jint g_vip_enabled = 0;

static jboolean JNICALL awrVipGate(JNIEnv *env, jobject thiz) {
    (void)env;
    (void)thiz;
    return g_vip_enabled ? JNI_TRUE : JNI_FALSE;
}

JNIEXPORT jboolean JNICALL
Java_com_awr_license_AwrLicenseInitializationContentProviderForHiTV2026_nativeSetVipEnabled(
        JNIEnv *env, jclass clazz, jboolean enabled) {
    (void)clazz;
    if (env == NULL) return JNI_FALSE;

    g_vip_enabled = enabled ? 1 : 0;

    jclass userState = (*env)->FindClass(env, "com/hitv/venom/store/user/UserState");
    if (userState == NULL) {
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
        return JNI_FALSE;
    }

    JNINativeMethod methods[] = {
        {"isMemberValid", "()Z", (void *)awrVipGate},
        {"vipEnableAd", "()Z", (void *)awrVipGate},
        {"vipEnableDownloadFast", "()Z", (void *)awrVipGate},
        {"vipEnableDownloadHd", "()Z", (void *)awrVipGate},
        {"vipEnableDownloadParallel", "()Z", (void *)awrVipGate},
        {"vipEnablePaidVideo", "()Z", (void *)awrVipGate},
        {"vipEnablePlayHd", "()Z", (void *)awrVipGate},
        {"vipEnableProjection", "()Z", (void *)awrVipGate},
        {"vipEnableTV", "()Z", (void *)awrVipGate},
        {"vipEnableTogetherVoice", "()Z", (void *)awrVipGate}
    };

    jint count = (jint)(sizeof(methods) / sizeof(methods[0]));
    jint rc = (*env)->RegisterNatives(env, userState, methods, count);
    if (rc != 0) {
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
        (*env)->DeleteLocalRef(env, userState);
        return JNI_FALSE;
    }

    (*env)->DeleteLocalRef(env, userState);
    return JNI_TRUE;
}
