#include "handles.h"

MemoryHandles::MemoryHandles() {}

FfiHandle MemoryHandles::registerHandle(const BYTE* ownedMemory)
{
    this->handlesCount++;
    FfiHandle currentHandle = this->handlesCount;
    this->handles[currentHandle] = ownedMemory;
    return currentHandle;
}

bool MemoryHandles::tryReleaseHandle(const FfiHandle handle)
{
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
    return this->handles.empty();
}