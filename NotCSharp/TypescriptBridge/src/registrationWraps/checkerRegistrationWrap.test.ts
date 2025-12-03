const newCheckerRegistrationWrap = require('./newCheckerRegistrationWrap')

test('correct injection', () => {
    // const registrationWrap = newCheckerRegistrationWrap()
    // registrationWrap.register({}, { warning: () => { }, error: () => { } })
    expect(newCheckerRegistrationWrap().register({}, { warning: () => { }, error: () => { } }))
});


