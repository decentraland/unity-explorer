#include <unordered_map>
#include <mutex>
#include "FreeImage.h"

typedef intptr_t FfiHandle;

class MemoryHandles
{

private:
    FfiHandle handlesCount;
    std::unordered_map<FfiHandle, const BYTE*> handles;
    mutable std::mutex mtx;

public:
    MemoryHandles();

    FfiHandle registerHandle(const BYTE *ownedMemory);

    bool tryReleaseHandle(const FfiHandle handle);

    bool empty() const;
};