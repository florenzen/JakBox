# Notes

## Development

* Build watcher:
```bash
yarn fable-splitter -c splitter.config.js -w --define DEBUG
```

* Run:
```bash
export JAVA_HOME=/Library/Java/JavaVirtualMachines/jdk1.8.0_51.jdk/Contents/Home
npx react-native run-android
```

* SQLite file on emulator device: `/data/data/com.jakbox/databases/repo.sqlite`
```bash
adb pull /data/data/com.jakbox/databases/repo.sqlite ~/tmp/repo1.sqlite
```

## Tasks

* Store duration of track
* Store first available track cover as album cover
