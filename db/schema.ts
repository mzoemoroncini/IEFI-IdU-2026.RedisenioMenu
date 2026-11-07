import { integer, sqliteTable, text } from 'drizzle-orm/sqlite-core';
export const restaurant = sqliteTable('restaurant_state', {id: text('id').primaryKey(),data: text('data').notNull(),version: integer('version').notNull().default(0)});
