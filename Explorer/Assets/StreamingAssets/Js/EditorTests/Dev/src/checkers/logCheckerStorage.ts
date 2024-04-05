import ICheckerStorage from "./checkerStorage";

export default function logCheckerStorage(
    storage: ICheckerStorage,
    log: (message: string) => void
): ICheckerStorage {
    return {
        checker: (name) => {
            log(`Requested checker: ${name}`)
            const item = storage.checker(name)
            return item
        }
    }
}