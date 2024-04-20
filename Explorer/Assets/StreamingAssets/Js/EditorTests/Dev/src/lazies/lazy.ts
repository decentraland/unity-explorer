interface Lazy<T> {
    get(): T
}

export function newLazy<T>(factoryFunction: () => T): Lazy<T> {
    let value: T | null = null

    return {
        get: () => {
            if (value === null) {
                value = factoryFunction()
            }
            return value
        }
    }
}