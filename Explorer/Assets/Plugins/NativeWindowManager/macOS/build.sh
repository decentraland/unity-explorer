#!/bin/bash
cd "$(dirname "$0")"
clang -dynamiclib -framework Cocoa -lobjc -arch arm64 -arch x86_64 \
    -o WindowResizeConstraint.dylib WindowResizeConstraint.mm
