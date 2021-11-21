#include <algorithm>        // std::swap
#include <functional>       // std::function
#include <iostream>
#include <stdlib.h>         // EXIT_FAILURE, EXIT_SUCCESS
#include <stdexcept>        // std::runtime_error, std::exception
#include <system_error>     // std::system_error
#include <string>           // std::string
using namespace std;

#include <conio.h>          // _kbhit

#undef UNICODE
#define UNICODE
#include <windows.h>
#include <shobjidl.h>       // IAppVisibilityEvents

auto fail(string const& message, int code = 0)
-> bool
{
    throw system_error(code, system_category(), message);
}

class On_block_exit
{
private:
    function<void()>    f_;
public:
    ~On_block_exit() { f_(); }
    On_block_exit(function<void()> f) : f_(f) {}
};

enum Success { success };

auto operator>>(HRESULT const hr, Success const)
-> bool
{
    ::SetLastError(hr);  return SUCCEEDED(hr);
}

template< class Interface >
class Com_ptr
{
private:
    Interface* p_;
public:
    Interface* pointer() const { return p_; }
    Interface* operator->() const { return p_; }

    void** as_out_arg()
    {
        *this = Com_ptr();
        return reinterpret_cast<void**>(&p_);
    }

    friend
        void swap(Com_ptr& a, Com_ptr& b) throw()
    {
        swap(a.p_, b.p_);
    }

    void operator=(Com_ptr rhs) { swap(*this, rhs); }

    ~Com_ptr() { if (p_) { p_->Release(); } }

    Com_ptr(Interface* const p = nullptr) : p_(p) {}

    Com_ptr(Com_ptr const& other)
        : p_(other.p_)
    {
        if (p_) { p_->AddRef(); }
    }
};

auto is_startscreen_shown(Com_ptr<IAppVisibility> p_app_visibility)
-> bool
{
    BOOL    result;
    p_app_visibility->IsLauncherVisible(&result)
        >> success
        || fail("IAppVisibility::IsLauncherVisible", ::GetLastError());
    return !!result;
}

template< class Base >
struct With_dummy_iunknown_
    : Base
{
    ULONG __stdcall AddRef() override { return 0; }

    ULONG __stdcall Release() override { return 0; }

    HRESULT __stdcall QueryInterface(REFIID riid, void** ppvObject) override
    {
        if (riid == IID_IUnknown)
        {
            *ppvObject = this;
            return S_OK;
        }
        return E_NOINTERFACE;
    }
};

void WriteToReqistry(int value)
{
    HKEY key;
    LONG openRes = RegOpenKeyEx(HKEY_LOCAL_MACHINE, L"SOFTWARE\\StartMenuDetection", 0, KEY_SET_VALUE, &key);

    if (openRes == ERROR_SUCCESS) {
        LONG setRes = RegSetValueEx(key, L"Open", 0, REG_DWORD, (BYTE*)&value, sizeof(value));

        if (setRes == ERROR_ACCESS_DENIED) {
            printf("Access denied writing to registry.");
        }
    }
    else {
        printf("Error reading registry.");
    }

    RegCloseKey(key);
}

void cpp_main()
{
    ::CoInitialize(nullptr)
        >> success
        || fail("CoInitialize", ::GetLastError());

    Com_ptr<IAppVisibility> p_app_visibility;

    ::CoCreateInstance(
        CLSID_AppVisibility, nullptr, CLSCTX_ALL, IID_IAppVisibility, p_app_visibility.as_out_arg()
    ) >> success
        || fail("CoCreateInstance CLSID_AppVisibility IID_IAppVisibility", ::GetLastError());

    cout << "Press the 'Any' key to quit." << endl;
    bool was_shown = false; bool first_iteration = true; int n = 0;
    for (;; )
    {
        if (_kbhit()) { break; }

        bool const is_shown = is_startscreen_shown(p_app_visibility);
        if (first_iteration || is_shown != was_shown)
        {
            cout << n << ": ";
            if (is_shown)
            {
                cout << "Currently showing the start screen" << endl;
                WriteToReqistry(1);
            }
            else
            {
                cout << "Currently showing normal desktop or app." << endl;
                WriteToReqistry(0);
            }
            ++n;
        }
        first_iteration = false;
        was_shown = is_shown;
        Sleep(50);
    }

#if 0
    struct Events : IAppVisibilityEvents
    {
        HRESULT __stdcall AppVisibilityOnMonitorChanged(
            HMONITOR                monitor_handle,
            MONITOR_APP_VISIBILITY  previous_mode,
            MONITOR_APP_VISIBILITY  current_mode
        ) override
        {
            return S_OK;
        }

        HRESULT __stdcall LauncherVisibilityChange(
            BOOL is_now_visible
        ) override
        {
            cout
                << (!!is_now_visible ? "Showing the start screen" : "Hiding start screen, showing desktop/app")
                << endl;
            return S_OK;
        }
    };

    With_dummy_iunknown_<Events> events;
    DWORD cookie = 1;
    p_app_visibility->Advise(&events, &cookie)
        >> success
        || fail("IAppVisibility::Advise", ::GetLastError());
    On_block_exit unadvise([=]() { p_app_visibility->Unadvise(cookie); });

    ::MessageBox(0, L"Press OK to quit.", L"Watching...", MB_SETFOREGROUND);
#endif
}

auto main()
-> int
{
    try
    {
        cpp_main();
        return EXIT_SUCCESS;
    }
    catch (system_error const& x)
    {
        cerr << "!" << x.what() << " (code " << x.code().value() << ")" << endl;
    }
    catch (exception const& x)
    {
        cerr << "!" << x.what() << endl;
    }
    return EXIT_FAILURE;
}