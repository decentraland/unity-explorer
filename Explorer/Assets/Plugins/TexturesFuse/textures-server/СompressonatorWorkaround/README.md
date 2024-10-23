To prepare the lib to build

<!-- Tested with Python 3.12 and 3.13 -->

1. Update git submodule for ./compressonator and ./externals_repos/simde
2. Run ./compressonator/build/fetch_dependencies.py
3. Run integrate_simde.py
4. Run builds_mac.sh
5. Copy libs to ../Compressator/lib