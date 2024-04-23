export default interface IntegrationTestsSource {
    get(key: string): ((context: IntegrationTestContext) => Promise<void>) | undefined
}

export type IntegrationTestContext = {
    result: any
    methodsBundle: any
}