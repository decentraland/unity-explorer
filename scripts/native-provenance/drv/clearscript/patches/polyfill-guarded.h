#ifndef INCLUDE_CLEARSCRIPT_POLYFILL_H_
#define INCLUDE_CLEARSCRIPT_POLYFILL_H_

#ifdef __linux__

#include <cstring>
#include <memory>
#include <sstream>
#include <utility>

namespace std {

  // make_unique_for_overwrite (nix-toolchain-compat: modern libstdc++ has it)
#if !defined(__cpp_lib_smart_ptr_for_overwrite)
  template <typename T>
  constexpr unique_ptr<T> make_unique_for_overwrite() {
    return unique_ptr<T>(new T);
  }
  template <typename T>
  constexpr unique_ptr<T> make_unique_for_overwrite(const size_t _Size) {
    return unique_ptr<T>(new remove_extent_t<T>[_Size]);
  }
#endif
  
  // format
  inline void __append_format(std::ostringstream& oss, const char* fmt) {
      oss << fmt;
  }
  template <typename T, typename... Rest>
  void __append_format(std::ostringstream& oss, const char* fmt, T&& value, Rest&&... rest) {
    const char* p = std::strstr(fmt, "{}");
    if (!p) {
        oss << fmt;
        return;
    }
    oss.write(fmt, p - fmt);
    oss << std::forward<T>(value);
    __append_format(oss, p + 2, std::forward<Rest>(rest)...);
  }
  template <typename... Args>
  std::string format(const char* fmt, Args&&... args) {
    std::ostringstream oss;
    __append_format(oss, fmt, std::forward<Args>(args)...);
    return oss.str();
  }

}

#endif // __linux__

#endif // INCLUDE_CLEARSCRIPT_POLYFILL_H_
