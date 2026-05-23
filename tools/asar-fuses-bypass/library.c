//
// Created by kitbyte on 30.11.2025.
//
// This proxy redefines the version.dll exports as forwarders, so the SDK's own
// prototypes must not be seen as dllimport (MinGW rejects redefining a
// dllimport-declared function as a "conflicting type"; MSVC tolerated it).
// WIN32_LEAN_AND_MEAN drops winver.h (Ver*/GetFileVersionInfo*) and defining
// WINBASEAPI empty strips the dllimport attribute from the winbase.h-declared
// VerFindFile*/VerInstallFile*. Exports are supplied by library.def.
#define WIN32_LEAN_AND_MEAN
#define WINBASEAPI
#include <windows.h>

extern BOOL disable_asar_integrity(void);

static HMODULE g_originalVersionDll;

#define FOR_EACH_VERSION_FORWARDER(X) \
    X(GetFileVersionInfoA, BOOL, FALSE, \
        (LPCSTR filename, DWORD handle, DWORD length, LPVOID data), \
        (filename, handle, length, data)) \
    X(GetFileVersionInfoExA, BOOL, FALSE, \
        (DWORD flags, LPCSTR filename, DWORD handle, DWORD length, LPVOID data), \
        (flags, filename, handle, length, data)) \
    X(GetFileVersionInfoExW, BOOL, FALSE, \
        (DWORD flags, LPCWSTR filename, DWORD handle, DWORD length, LPVOID data), \
        (flags, filename, handle, length, data)) \
    X(GetFileVersionInfoSizeA, DWORD, 0, \
        (LPCSTR filename, LPDWORD handle), \
        (filename, handle)) \
    X(GetFileVersionInfoSizeExA, DWORD, 0, \
        (DWORD flags, LPCSTR filename, LPDWORD handle), \
        (flags, filename, handle)) \
    X(GetFileVersionInfoSizeExW, DWORD, 0, \
        (DWORD flags, LPCWSTR filename, LPDWORD handle), \
        (flags, filename, handle)) \
    X(GetFileVersionInfoSizeW, DWORD, 0, \
        (LPCWSTR filename, LPDWORD handle), \
        (filename, handle)) \
    X(GetFileVersionInfoW, BOOL, FALSE, \
        (LPCWSTR filename, DWORD handle, DWORD length, LPVOID data), \
        (filename, handle, length, data)) \
    /* Ver*File* use non-const LPSTR/LPWSTR to match MinGW's winver.h prototypes. */ \
    X(VerFindFileA, DWORD, 0, \
        (DWORD flags, LPSTR fileName, LPSTR winDir, LPSTR appDir, LPSTR curDir, PUINT curDirLen, LPSTR destDir, PUINT destDirLen), \
        (flags, fileName, winDir, appDir, curDir, curDirLen, destDir, destDirLen)) \
    X(VerFindFileW, DWORD, 0, \
        (DWORD flags, LPWSTR fileName, LPWSTR winDir, LPWSTR appDir, LPWSTR curDir, PUINT curDirLen, LPWSTR destDir, PUINT destDirLen), \
        (flags, fileName, winDir, appDir, curDir, curDirLen, destDir, destDirLen)) \
    X(VerInstallFileA, DWORD, 0, \
        (DWORD flags, LPSTR srcFileName, LPSTR destFileName, LPSTR srcDir, LPSTR destDir, LPSTR curDir, LPSTR tempFile, PUINT tempFileLen), \
        (flags, srcFileName, destFileName, srcDir, destDir, curDir, tempFile, tempFileLen)) \
    X(VerInstallFileW, DWORD, 0, \
        (DWORD flags, LPWSTR srcFileName, LPWSTR destFileName, LPWSTR srcDir, LPWSTR destDir, LPWSTR curDir, LPWSTR tempFile, PUINT tempFileLen), \
        (flags, srcFileName, destFileName, srcDir, destDir, curDir, tempFile, tempFileLen)) \
    X(VerLanguageNameA, DWORD, 0, \
        (DWORD language, LPSTR buffer, DWORD bufferLength), \
        (language, buffer, bufferLength)) \
    X(VerLanguageNameW, DWORD, 0, \
        (DWORD language, LPWSTR buffer, DWORD bufferLength), \
        (language, buffer, bufferLength)) \
    X(VerQueryValueA, BOOL, FALSE, \
        (LPCVOID block, LPCSTR subBlock, LPVOID* buffer, PUINT bufferLength), \
        (block, subBlock, buffer, bufferLength)) \
    X(VerQueryValueW, BOOL, FALSE, \
        (LPCVOID block, LPCWSTR subBlock, LPVOID* buffer, PUINT bufferLength), \
        (block, subBlock, buffer, bufferLength))

#if defined(_MSC_VER) && !defined(_WIN64)

#define DECLARE_FORWARDER(name, return_type, default_value, params, args) \
    static FARPROC s_##name; \
    __declspec(naked) return_type WINAPI name params \
    { \
        __asm \
        { \
            jmp dword ptr [s_##name] \
        } \
    }

#define LOAD_FORWARDER(name, return_type, default_value, params, args) \
    s_##name = GetProcAddress(g_originalVersionDll, #name);

#else

#define DECLARE_FORWARDER(name, return_type, default_value, params, args) \
    typedef return_type (WINAPI *name##_fn) params; \
    static name##_fn s_##name; \
    return_type WINAPI name params \
    { \
        if (s_##name == NULL) \
        { \
            SetLastError(ERROR_PROC_NOT_FOUND); \
            return default_value; \
        } \
        return s_##name args; \
    }

#define LOAD_FORWARDER(name, return_type, default_value, params, args) \
    s_##name = (name##_fn)GetProcAddress(g_originalVersionDll, #name);

#endif

FOR_EACH_VERSION_FORWARDER(DECLARE_FORWARDER)

BOOL WINAPI GetFileVersionInfoByHandle(void)
{
    SetLastError(ERROR_CALL_NOT_IMPLEMENTED);
    return FALSE;
}

static BOOL SourceInit(void)
{
    WCHAR source[MAX_PATH];
    UINT sourceLength = GetSystemDirectoryW(source, MAX_PATH);

    if (sourceLength == 0 || sourceLength >= MAX_PATH)
    {
        return FALSE;
    }

    if (wcscat_s(source, MAX_PATH, L"\\version.dll") != 0)
    {
        return FALSE;
    }

    g_originalVersionDll = LoadLibraryW(source);
    if (!g_originalVersionDll)
    {
        return FALSE;
    }

    FOR_EACH_VERSION_FORWARDER(LOAD_FORWARDER);

    return TRUE;
}

BOOL WINAPI DllMain(HMODULE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    (void)lpvReserved;

    if (fdwReason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hinstDLL);

        if (!SourceInit())
        {
            return FALSE;
        }

        disable_asar_integrity();
    }

    return TRUE;
}