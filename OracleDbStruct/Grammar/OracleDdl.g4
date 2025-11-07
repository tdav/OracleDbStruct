grammar OracleDdl;

sql_script
    :   (statement | SEMI)+ EOF
    ;

statement
    :   CREATE .*? SEMI
    |   .*? SEMI
    ;

CREATE: [cC][rR][eE][aA][tT][eE];
OR: [oO][rR];
REPLACE: [rR][eE][pP][lL][aA][cC][eE];
TABLE: [tT][aA][bB][lL][eE];
VIEW: [vV][iI][eE][wW];
MATERIALIZED: [mM][aA][tT][eE][rR][iI][aA][lL][iI][zZ][eE][dD];
PACKAGE: [pP][aA][cC][kK][aA][gG][eE];
BODY: [bB][oO][dD][yY];
FUNCTION: [fF][uU][nN][cC][tT][iI][oO][nN];
PROCEDURE: [pP][rR][oO][cC][eE][dD][uU][rR][eE];
TRIGGER: [tT][rR][iI][gG][gG][eE][rR];
AS: [aA][sS];
IS: [iI][sS];
BEGIN: [bB][eE][gG][iI][nN];
END: [eE][nN][dD];
SELECT: [sS][eE][lL][eE][cC][tT];
FROM: [fF][rR][oO][mM];
JOIN: [jJ][oO][iI][nN];
INNER: [iI][nN][nN][eE][rR];
LEFT: [lL][eE][fF][tT];
RIGHT: [rR][iI][gG][hH][tT];
FULL: [fF][uU][lL][lL];
OUTER: [oO][uU][tT][eE][rR];
CROSS: [cC][rR][oO][sS][sS];
ON: [oO][nN];
USING: [uU][sS][iI][nN][gG];
WHERE: [wW][hH][eE][rR][eE];
INSERT: [iI][nN][sS][eE][rR][tT];
INTO: [iI][nN][tT][oO];
UPDATE: [uU][pP][dD][aA][tT][eE];
DELETE: [dD][eE][lL][eE][tT][eE];
MERGE: [mM][eE][rR][gG][eE];
REFERENCES: [rR][eE][fF][eE][rR][eE][nN][cC][eE][sS];
FOREIGN: [fF][oO][rR][eE][iI][gG][nN];
KEY: [kK][eE][yY];
PRIMARY: [pP][rR][iI][mM][aA][rR][yY];
UNIQUE: [uU][nN][iI][qQ][uU][eE];
NOT: [nN][oO][tT];
NULL: [nN][uU][lL][lL];
WITH: [wW][iI][tT][hH];
UNION: [uU][nN][iI][oO][nN];
ALL: [aA][lL][lL];
MINUS: [mM][iI][nN][uU][sS];
INTERSECT: [iI][nN][tT][eE][rR][sS][eE][cC][tT];
VALUES: [vV][aA][lL][uU][eE][sS];
SET: [sS][eE][tT];
OF: [oO][fF];
ONLY: [oO][nN][lL][yY];
SEMI: ';';
COMMA: ',';
DOT: '.';
LPAREN: '(';
RPAREN: ')';
STAR: '*';
PLUS: '+';
MINUS_SIGN: '-';
DIVIDE: '/';
EQ: '=';
LT: '<';
GT: '>';
COLON: ':';
ASSIGN: ':=';
AT: '@';
PIPE: '|';
AMPERSAND: '&';
PERCENT: '%';
CARET: '^';
STRING: '\'' ( '\'\'' | ~'\'' )* '\'';
QUOTED_IDENTIFIER: '"' ( '""' | ~'"' )* '"';
IDENTIFIER: [A-Za-z_#\$][A-Za-z0-9_#\$]*;
NUMBER: [0-9]+ ('.' [0-9]+)?;
WS: [ \t\r\n]+ -> skip;
COMMENT: '--' ~[\r\n]* -> skip;
MULTILINE_COMMENT: '/*' .*? '*/' -> skip;
UNEXPECTED_CHAR: . -> skip;
EOF_: EOF;
 