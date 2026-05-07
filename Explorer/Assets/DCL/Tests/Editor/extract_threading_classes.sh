# used for ShouldNotUseThreadingApiDirectly test

curl -s 'https://learn.microsoft.com/en-us/dotnet/api/system.threading?view=net-10.0' |

rg 'class="xref" href="system.threading.' |

sed 's/.*data-linktype="relative-path">//' |

# remove characters after the search
sed 's/<.*//' |

# Allowed API, that are safe for WebGL
grep -v 'CancellationToken' |
grep -v 'Timeout' |
grep -v 'ThreadStateException' |

# Output
cat > excludes_threading.txt

echo 'Successfully updated the excludes_threading.txt file'

# remove namespaces
#sed 's/^system\.threading\.//'
