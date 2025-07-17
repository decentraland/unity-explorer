# Unity Explorer for Decentraland 2.0

![Decentraland Logo](https://decentraland.org/images/logo.png)

Unity Explorer is the official desktop client implementation for Decentraland 2.0, allowing users to explore and interact with the Decentraland metaverse using Unity. This desktop client delivers a smoother, more immersive experience compared to the previous web-based version, setting the foundation for Decentraland's future expansion!

## 🌟 Features

### Improved Performance
- Significantly faster load times and increased frame rates
- Smoother experience in crowded scenes
- Refined multiplayer gameplay.
- Cross-platform compatibility (Windows & Mac)

### Immersive Environment
- Enhanced graphics with extended draw distance.
- Dual sun/moon system matching Decentraland's iconic logo.
- Procedurally-generated landscapes for undeveloped parcels.
- Detailed environmental effects (ocean, trees, ambience)
- Support for worlds.

### Enhanced Avatars & Social Interactions
- More natural avatar movements and environmental interactions
- In-world chat bubbles with emoji support
- Integration with Decentraland's NFT wearables

### Gameplay & Engagement
- Badge system to track and showcase achievements
- Daily quests and challenge system
- Integration with mini-games throughout Decentraland
- Daily rewards (Wearables and Emotes)

### Developer-Friendly
- Unity-based development environment
- Support for decentralized content creation and deployment
- Integration with Creator Hub resources

## 📋 Requirements

- Unity 6000.0.43f1

## 🚀 Installation & Setup

1. Clone the repository:
   ```
   git clone https://github.com/decentraland/unity-explorer.git
   ```

2. Pull the submodules:
   ```
   git submodule update --init --recursive
   ```

2. Open the project in Unity

## 🎮 Quick Start

- After installation, open Unity-Explorer via Unity.
- Authenticate via MetaMask

## 📚 Documentation

For detailed information about the project, please visit our [Wiki](https://github.com/decentraland/unity-explorer/wiki).

### Architecture

The Unity Explorer follows a component-based architecture designed for flexibility and scalability. Learn more in our [Architecture Overview](https://github.com/decentraland/unity-explorer/wiki/Architecture-Overview).

### Development Guides

Find specific guidance on development topics in our [How To Guide](https://github.com/decentraland/unity-explorer/wiki/How-To).

## 🔧 Troubleshooting

### Plugins not compiling (e.g. "The type or namespace name 'Google' could not be found" error)

That happens if you haven't got GIT LFS installed. 

A simple way to confirm that is looking at this [Google.Api.CommonProtos.dll file](https://github.com/decentraland/unity-explorer/blob/50ddf83a3ff7eb76c6036904390d3298a24e2f88/Explorer/Assets/Protocol/Plugins/Google.Api.CommonProtos.dll) here in Github and compare its size (348KB) with the size of the one you have in your cloned version of the repo (it would be a 'placeholder' file with only 131 bytes aprox).

1. Make sure you have git-lfs installed, for example you can install it with `brew install git-lfs`.
2. Close Unity and step in the cloned repo root directory.
3. run `git lfs install`, `git lfs pull` and `git submodule update --init --recursive`.
4. Just in case delete the `Explorer/Library/` folder.
5. Open the Unity project again and this time it should compile correctly

## 🛣️ Roadmap

See our [Whitepaper](https://decentraland.org/blog/announcements/decentralands-white-paper-2-0)

## 👥 Contributing

Please follow our coding standards and guidelines outlined in our [How To Guide](https://github.com/decentraland/unity-explorer/wiki/How-To).

## 🤝 Community and Support

- [Discord Server](https://discord.gg/decentraland)
- [Forum](https://forum.decentraland.org/)
- [Twitter](https://twitter.com/decentraland)

## 📜 License

This project is licensed under the [Apache 2.0 License](LICENSE.md) - see the LICENSE file for details.
