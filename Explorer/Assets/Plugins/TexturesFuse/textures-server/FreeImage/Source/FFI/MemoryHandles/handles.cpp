#include "handles.h"
#include <mutex>

MemoryHandles::MemoryHandles() {}

FfiHandle MemoryHandles::registerHandle(const BYTE* ownedMemory)
{
    std::lock_guard<std::mutex> lock(this->mtx);
    this->handlesCount++;
    FfiHandle currentHandle = this->handlesCount;
    this->handles[currentHandle] = ownedMemory;
    return currentHandle;
}

bool MemoryHandles::tryReleaseHandle(const FfiHandle handle)
{
    std::lock_guard<std::mutex> lock(this->mtx);
    if (this->handles.find(handle) == this->handles.end())
    {
        return false;
    }

    const BYTE* memory = handles[handle];
    handles.erase(handle);
    delete[] memory;

    return true;
}

bool MemoryHandles::empty() const
{
    std::lock_guard<std::mutex> lock(this->mtx);
    return this->handles.empty();
}