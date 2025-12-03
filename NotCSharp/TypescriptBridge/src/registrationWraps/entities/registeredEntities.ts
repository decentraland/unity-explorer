export interface RegisteredEntities {
    add(key: string): void
    has(key: string): boolean
}

export function newFakeRegisteredEntities(): RegisteredEntities {
    return {
        add: () => { },
        has: () => false
    }
}